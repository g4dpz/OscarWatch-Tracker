using System.Collections.Concurrent;
using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using OscarWatch.Core.Services;
using Serilog;

namespace OscarWatch.Rig;

/// <summary>
/// All rig I/O runs on a dedicated background thread; the UI only enqueues commands and reads status.
/// </summary>
public sealed class RigController : IRigController, IDisposable
{
    private static readonly ILogger Log = Serilog.Log.ForContext<RigController>();
    private const int DialHistoryLength = 3;
    private const int FmCompanionLegHz = 10;
    /// <summary>Min Hz between dial and last CAT RX to treat as operator tuning (QTrigdoppler uses 1 Hz).</summary>
    private const int KnobTuneCaptureThresholdHz = 1;
    /// <summary>After a CAT frequency write, ignore dial stability briefly so reads settle.</summary>
    private const int PostCatWriteDialSettleMs = 350;
    private static readonly TimeSpan LoopInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan CommandWaitTimeout = TimeSpan.FromSeconds(10);

    private readonly Func<RigSettings, IRigDriver>? _driverFactory;
    private readonly long[] _rxDialHistory = new long[DialHistoryLength];
    private readonly object _statusLock = new();
    private readonly object _workerStartLock = new();

    private BlockingCollection<RigCommand>? _commands;
    private Thread? _worker;
    private int _disposed;
    private volatile bool _shutdownRequested;

    private IRigDriver? _driver;
    private string? _connectedKey;
    private string? _passKey;
    private long _lastRigRxHz;
    private long _lastRigTxHz;
    private long _displayRxHz;
    private long _displayTxHz;
    private DateTime _lastWriteUtc = DateTime.MinValue;
    private int _thresholdHz;
    private bool _interactive;
    private bool _useMainSub;
    private bool _isBeaconOnly;
    private RigVfo _receiveVfo = RigVfo.VfoA;
    private int _rxDialHistoryCount;
    private bool _vfoNotMoving;
    private double _passbandDownlinkAdjustKHz;
    private double _passbandUplinkAdjustKHz;
    private string? _statusMessage;
    private bool _isTracking;
    private bool _catUpdatesPaused;
    private bool _passInitPending;
    private double? _lastAppliedCtcssHz;
    private bool? _lastAppliedCtcssSquelch;
    private double _lastContextRxOffsetKHz;
    private bool _forceFrequencyApply;
    private bool _blockKnobCapture;
    private DateTime _ignoreDialUntilUtc = DateTime.MinValue;
    private DateTime _suspendDopplerUntilUtc = DateTime.MinValue;
    private bool? _lastPassDownlinkOnVhf;

    private RigSettings _cachedSettings = new();
    private RigTrackingContext? _cachedContext;
    private RigConnectionStatus _status = new(false, false, null, null, null, false, 0, 0);

    public RigController(Func<RigSettings, IRigDriver>? driverFactory = null) =>
        _driverFactory = driverFactory;

    public RigConnectionStatus GetStatus()
    {
        lock (_statusLock)
            return _status;
    }

    /// <summary>Enqueue latest pass/settings for the rig thread (~1–4 Hz from UI).</summary>
    public void PublishContext(RigSettings settings, RigTrackingContext? context, bool reinitializePass = false) =>
        Enqueue(new RigCommand(RigCommandKind.PublishContext, settings, context, reinitializePass));

    /// <summary>Runs one doppler iteration on the rig thread (unit tests).</summary>
    public void RunTrackingLoopOnce() =>
        EnqueueAndWait(new RigCommand(RigCommandKind.RunTrackingLoopOnce));

    /// <summary>Synchronous publish + doppler tick (unit tests).</summary>
    public void Update(RigSettings settings, RigTrackingContext? context) =>
        EnqueueAndWait(new RigCommand(RigCommandKind.UpdateSynchronously, settings, context));

    public void ApplySelectedCtcss(RigSettings settings, RigTrackingContext? context)
    {
        if (context is null)
            return;

        Enqueue(new RigCommand(RigCommandKind.ApplySelectedCtcss, settings, context));
    }

    public void Disconnect() =>
        Enqueue(new RigCommand(RigCommandKind.Disconnect));

    /// <summary>Blocks until queued commands are processed (unit tests).</summary>
    internal void DrainCommandQueueForTests() =>
        EnqueueAndWait(new RigCommand(RigCommandKind.Drain));

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        try
        {
            if (_commands is not null && _worker is { IsAlive: true })
                EnqueueAndWait(new RigCommand(RigCommandKind.Shutdown), TimeSpan.FromSeconds(3));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Rig worker shutdown did not complete cleanly");
        }

        _commands?.Dispose();
        _commands = null;
        _worker?.Join(TimeSpan.FromSeconds(2));
    }

    private void Enqueue(RigCommand command)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        EnsureWorker();
        _commands!.Add(command);
    }

    private void EnqueueAndWait(RigCommand command, TimeSpan? timeout = null)
    {
        using var done = new ManualResetEventSlim(false);
        command.Completed = done;
        Enqueue(command);
        if (!done.Wait(timeout ?? CommandWaitTimeout))
            throw new TimeoutException("Rig worker did not complete the command in time.");
    }

    private void EnsureWorker()
    {
        lock (_workerStartLock)
        {
            if (_worker is { IsAlive: true })
                return;

            _shutdownRequested = false;
            _commands = new BlockingCollection<RigCommand>();
            _worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "OscarWatch.Rig"
            };
            _worker.Start();
        }
    }

    private void WorkerLoop()
    {
        try
        {
            while (!_shutdownRequested)
            {
                if (_commands!.TryTake(out var command, LoopInterval))
                {
                    ProcessCommand(command);
                    DrainPendingCommands();
                }

                if (_shutdownRequested)
                    break;

                RunLoopIteration(ignoreDopplerSuspend: false);
                RefreshStatusSnapshot();
            }
        }
        finally
        {
            TearDownRig();
            RefreshStatusSnapshot();
        }
    }

    private void DrainPendingCommands()
    {
        while (_commands!.TryTake(out var command, 0))
            ProcessCommand(command);
    }

    private void ProcessCommand(RigCommand command)
    {
        try
        {
            switch (command.Kind)
            {
                case RigCommandKind.PublishContext:
                    _cachedSettings = command.Settings;
                    _cachedContext = command.Context;
                    ApplyPublishState(_cachedSettings, _cachedContext, command.ReinitializePass);
                    if (!command.ReinitializePass && _forceFrequencyApply)
                        RunLoopIteration(ignoreDopplerSuspend: true);
                    break;

                case RigCommandKind.UpdateSynchronously:
                    _cachedSettings = command.Settings;
                    _cachedContext = command.Context;
                    ApplyPublishState(_cachedSettings, _cachedContext);
                    RunLoopIteration(ignoreDopplerSuspend: true);
                    break;

                case RigCommandKind.RunTrackingLoopOnce:
                    RunLoopIteration(ignoreDopplerSuspend: true);
                    break;

                case RigCommandKind.ApplySelectedCtcss:
                    ApplySelectedCtcssOnWorker(command.Settings, command.Context!);
                    break;

                case RigCommandKind.Disconnect:
                    _cachedContext = null;
                    TearDownRig();
                    ResetTrackingState();
                    break;

                case RigCommandKind.Drain:
                    break;

                case RigCommandKind.Shutdown:
                    _shutdownRequested = true;
                    break;
            }
        }
        finally
        {
            RefreshStatusSnapshot();
            command.Completed?.Set();
        }
    }

    private void ApplySelectedCtcssOnWorker(RigSettings settings, RigTrackingContext context)
    {
        if (!settings.Enabled || settings.Type == RigType.None)
            return;

        if (string.IsNullOrWhiteSpace(settings.Port) && settings.Type != RigType.Dummy)
            return;

        if (!EnsureConnected(settings))
            return;

        if (context.SelectedCtcssHz is not { } hz || hz <= 0 || context.Mode.IsBeaconOnly)
            return;

        _useMainSub = RigSatModeHelper.UseMainSubLayout(context.Mode.DownlinkKHz, context.Mode.UplinkKHz);
        ApplyCtcss(settings, context, force: true);
        RestoreOperatorVfo();
    }

    private void RefreshStatusSnapshot()
    {
        var snapshot = new RigConnectionStatus(
            _driver?.IsConnected == true,
            _isTracking,
            _statusMessage,
            DisplayHz(_displayRxHz, _lastRigRxHz),
            DisplayHz(_displayTxHz, _lastRigTxHz),
            _catUpdatesPaused,
            _passbandDownlinkAdjustKHz,
            _passbandUplinkAdjustKHz);

        lock (_statusLock)
            _status = snapshot;
    }

    private void ApplyPublishState(RigSettings settings, RigTrackingContext? context, bool reinitializePass = false)
    {
        if (!settings.Enabled || settings.Type == RigType.None)
        {
            TearDownRig();
            _statusMessage = null;
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.Port) && settings.Type != RigType.Dummy)
        {
            TearDownRig();
            _statusMessage = "No COM port selected";
            return;
        }

        if (!EnsureConnected(settings))
        {
            _statusMessage = "Rig not connected";
            return;
        }

        var resumingFromCatPause = _catUpdatesPaused && !settings.CatUpdatesPaused;
        _catUpdatesPaused = settings.CatUpdatesPaused;

        if (context is not null)
            SyncDisplayFrequencies(context);

        if (context is null || context.TrackState.LookAngles is null)
        {
            _isTracking = false;
            _statusMessage = settings.CatUpdatesPaused ? "CAT paused (manual tuning)" : "Connected";
            return;
        }

        _isBeaconOnly = context.Mode.IsBeaconOnly;

        if (_driver is null || !_driver.SupportsTracking)
            return;

        var newPassKey = PassKey(context);
        var passKeyChanged = !string.Equals(_passKey, newPassKey, StringComparison.Ordinal);
        if (passKeyChanged)
            BeginNewPass(settings, context, newPassKey);

        if (settings.CatUpdatesPaused)
        {
            _isTracking = false;
            _statusMessage = "CAT paused (manual tuning)";
            return;
        }

        if (resumingFromCatPause || _passInitPending)
        {
            RunPassInit(settings, context);
            _passInitPending = false;
        }
        else if (reinitializePass && !passKeyChanged)
            RunPassInit(settings, context);
        else if (context.SelectedCtcssHz is > 0)
            ApplyCtcss(settings, context, force: false);

        NoteContextOffsetChange(context);
        _isTracking = true;
        _statusMessage = "Tracking";
    }

    private void BeginNewPass(RigSettings settings, RigTrackingContext context, string newPassKey)
    {
        _passKey = newPassKey;
        _passbandDownlinkAdjustKHz = 0;
        _passbandUplinkAdjustKHz = 0;
        ClearDialHistory();
        _lastAppliedCtcssHz = null;
        _lastAppliedCtcssSquelch = null;
        _lastContextRxOffsetKHz = context.ReceiveOffsetKHz;
        _forceFrequencyApply = false;

        if (settings.CatUpdatesPaused)
            _passInitPending = true;
        else
            RunPassInit(settings, context);
    }

    private void TearDownRig()
    {
        _driver?.Dispose();
        _driver = null;
        _connectedKey = null;
        ResetTrackingState();
    }

    private void ResetTrackingState()
    {
        _passKey = null;
        _lastRigRxHz = 0;
        _lastRigTxHz = 0;
        _displayRxHz = 0;
        _displayTxHz = 0;
        _isTracking = false;
        ClearDialHistory();
        _passInitPending = false;
        _catUpdatesPaused = false;
        _lastAppliedCtcssHz = null;
        _lastAppliedCtcssSquelch = null;
        _lastPassDownlinkOnVhf = null;
        _receiveVfo = RigVfo.VfoA;
        _suspendDopplerUntilUtc = DateTime.MinValue;
    }

    private void RunLoopIteration(bool ignoreDopplerSuspend = false)
    {
        if (_driver is null || !_driver.IsConnected || _cachedContext is null)
            return;

        if (!_cachedSettings.Enabled || _cachedSettings.CatUpdatesPaused || !_isTracking)
            return;

        if (!ignoreDopplerSuspend && DateTime.UtcNow < _suspendDopplerUntilUtc)
            return;

        if (_cachedContext.TrackState.LookAngles is null)
            return;

        if (_interactive && SetupVfosPolicy.IsLinearMode(_cachedContext.Mode.DownlinkMode))
            ProcessInteractiveLinear(_cachedSettings, _cachedContext);
        else
            ProcessAutomaticDoppler(_cachedSettings, _cachedContext);
    }

    private void ProcessInteractiveLinear(RigSettings settings, RigTrackingContext context)
    {
        SampleReceiveDial();
        SyncManualFromMainDial(context);

        if (!_forceFrequencyApply && !_vfoNotMoving)
        {
            RestoreOperatorVfo();
            return;
        }

        WriteDopplerFrequencies(settings, context);
        RestoreOperatorVfo();
    }

    private void ProcessAutomaticDoppler(RigSettings settings, RigTrackingContext context)
    {
        WriteDopplerFrequencies(settings, context);
        RestoreOperatorVfo();
    }

    /// <summary>
    /// Derive manual RX/TX adjust from Main dial vs doppler baseline (not vs last CAT write).
    /// Clears phantom manual state when the dial matches the computed target.
    /// </summary>
    private void SyncManualFromMainDial(RigTrackingContext context)
    {
        if (_blockKnobCapture || DateTime.UtcNow < _ignoreDialUntilUtc || !_vfoNotMoving
            || context.TrackState.LookAngles is null
            || !TryReadReceiveDialHz(out var dialHz))
            return;

        var baseline = DopplerFrequencyCalculator.Compute(
            context.Mode,
            context.TrackState.LookAngles.RangeRateKmPerSec,
            context.ReceiveOffsetKHz,
            _passbandDownlinkAdjustKHz,
            _passbandUplinkAdjustKHz);
        var pureBaselineHz = ToHz(DopplerFrequencyCalculator.Compute(
            context.Mode,
            context.TrackState.LookAngles.RangeRateKmPerSec,
            context.ReceiveOffsetKHz,
            0,
            0).RadioReceiveKHz);
        var expectedMainHz = ToHz(baseline.RadioReceiveKHz);
        var deltaFromBaselineHz = dialHz - expectedMainHz;
        var threshold = KnobTuneThresholdHz();

        if (Math.Abs(dialHz - pureBaselineHz) < threshold
            && (Math.Abs(_passbandDownlinkAdjustKHz) > 0.0001 || Math.Abs(_passbandUplinkAdjustKHz) > 0.0001))
        {
            _passbandDownlinkAdjustKHz = 0;
            _passbandUplinkAdjustKHz = 0;
            _forceFrequencyApply = true;
            return;
        }

        if (Math.Abs(deltaFromBaselineHz) < threshold)
            return;

        // Dial still matches the last CAT write — baseline moved (doppler lag), not a knob tune.
        if (_lastRigRxHz > 0 && Math.Abs(dialHz - _lastRigRxHz) < threshold)
            return;

        var deltaKhz = deltaFromBaselineHz / 1000.0;
        double newDown;
        double newUp;
        if (context.Mode.DopplerCorrection == DopplerCorrection.Reverse)
        {
            // REV: Main dial up → downlink nominal up, uplink nominal down.
            newDown = _passbandDownlinkAdjustKHz + deltaKhz;
            newUp = _passbandUplinkAdjustKHz - deltaKhz;
        }
        else
        {
            // NOR: both nominals move with Main dial.
            newDown = _passbandDownlinkAdjustKHz + deltaKhz;
            newUp = _passbandUplinkAdjustKHz + deltaKhz;
        }

        if (NearlyEqual(newDown, _passbandDownlinkAdjustKHz) && NearlyEqual(newUp, _passbandUplinkAdjustKHz))
            return;

        _passbandDownlinkAdjustKHz = newDown;
        _passbandUplinkAdjustKHz = newUp;
        SeedDialHistoryStable(dialHz);
        _vfoNotMoving = true;
    }

    private void SeedDialHistoryStable(long dialHz)
    {
        for (var i = 0; i < DialHistoryLength; i++)
            _rxDialHistory[i] = dialHz;

        _rxDialHistoryCount = DialHistoryLength;
    }

    private static int KnobTuneThresholdHz() => KnobTuneCaptureThresholdHz;

    private void SampleReceiveDial()
    {
        if (DateTime.UtcNow < _ignoreDialUntilUtc)
        {
            _vfoNotMoving = false;
            return;
        }

        if (!TryReadReceiveDialHz(out var dialHz))
        {
            _vfoNotMoving = false;
            return;
        }

        ShiftDialHistory(dialHz);
        _vfoNotMoving = _rxDialHistoryCount >= DialHistoryLength
            && _rxDialHistory[0] == _rxDialHistory[1]
            && _rxDialHistory[1] == _rxDialHistory[2];
    }

    private void ShiftDialHistory(long dialHz)
    {
        if (_rxDialHistoryCount < DialHistoryLength)
        {
            _rxDialHistory[_rxDialHistoryCount++] = dialHz;
            return;
        }

        for (var i = 0; i < DialHistoryLength - 1; i++)
            _rxDialHistory[i] = _rxDialHistory[i + 1];

        _rxDialHistory[DialHistoryLength - 1] = dialHz;
    }

    private void ClearDialHistory()
    {
        _rxDialHistoryCount = 0;
        _vfoNotMoving = false;
        Array.Clear(_rxDialHistory);
    }

    private void WriteDopplerFrequencies(RigSettings settings, RigTrackingContext context)
    {
        var corrected = ComputeDoppler(settings, context, usePredictive: true);

        SyncDisplayFrequencies(context);

        var rxHz = ToHz(corrected.RadioReceiveKHz);
        var txHz = ToHz(corrected.RadioTransmitKHz);

        if (!CanWrite(settings))
            return;

        var forceApply = _forceFrequencyApply;
        _forceFrequencyApply = false;
        var thresholdHz = EffectiveLinearThresholdHz(settings, context);
        if (!forceApply && !ShouldWrite(thresholdHz, rxHz, txHz))
            return;

        var rxDelta = Math.Abs(rxHz - _lastRigRxHz);
        var txDelta = _isBeaconOnly ? 0 : Math.Abs(txHz - _lastRigTxHz);
        var writeRx = forceApply || rxDelta > thresholdHz || thresholdHz == 0;
        var writeTx = !_isBeaconOnly && (forceApply || txDelta > thresholdHz || thresholdHz == 0);

        // Cross-band: keep RX/TX CAT in sync when either leg triggers (FM automatic, or linear interactive).
        if (!forceApply)
        {
            var crossBand = RigSatModeHelper.UseMainSubLayout(context.Mode.DownlinkKHz, context.Mode.UplinkKHz);
            if (crossBand)
            {
                var companionHz = _interactive ? 0 : FmCompanionLegHz;
                if (writeRx && !writeTx && txDelta > companionHz)
                    writeTx = true;
                if (writeTx && !writeRx && rxDelta > companionHz)
                    writeRx = true;
            }
        }

        var wrote = false;
        if (writeRx)
        {
            WriteRx(rxHz);
            wrote = true;
        }

        if (writeTx)
        {
            WriteTx(txHz);
            wrote = true;
        }

        _lastWriteUtc = DateTime.UtcNow;
        if (wrote)
        {
            MarkProgrammaticFrequencySettle();
            FinishOffsetKnobCaptureBlock();
        }
    }

    private void FinishOffsetKnobCaptureBlock()
    {
        if (!_blockKnobCapture)
            return;

        if (TryReadReceiveDialHz(out var dialHz))
            _lastRigRxHz = dialHz;

        _blockKnobCapture = false;
    }

    private static long? DisplayHz(long displayHz, long lastWrittenHz) =>
        displayHz > 0 ? displayHz : lastWrittenHz > 0 ? lastWrittenHz : null;

    private bool EnsureConnected(RigSettings settings)
    {
        var key = $"{settings.Type}|{settings.Port}|{settings.BaudRate}|{settings.CivAddress}";
        if (_driver is not null && _connectedKey == key && _driver.IsConnected)
            return true;

        _driver?.Dispose();
        try
        {
            _driver = (_driverFactory ?? RigDriverFactory.Create)(settings);
            _driver.Open();
            _connectedKey = key;
            return _driver.IsConnected;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Rig connect failed for {RigType} on {Port}", settings.Type, settings.Port);
            _driver?.Dispose();
            _driver = null;
            return false;
        }
    }

    private void RunPassInit(RigSettings settings, RigTrackingContext context)
    {
        if (_driver is null)
            return;

        _useMainSub = !_isBeaconOnly
            && RigSatModeHelper.UseMainSubLayout(context.Mode.DownlinkKHz, context.Mode.UplinkKHz);
        AssignReceiveVfo(settings, context);

        if (_isBeaconOnly)
        {
            _driver.SetSatelliteMode(false);
            _driver.SetSplitOn(false);
            ClearCtcssLeavingSatelliteMode(settings);
            EnsureBeaconDownlinkOnMain(context);
        }
        else if (!_useMainSub)
        {
            _driver.SetSatelliteMode(false);
            _driver.SetSplitOn(true);
            ClearCtcssLeavingSatelliteMode(settings);
        }
        else
        {
            _driver.SetSatelliteMode(true);
            _driver.SetSplitOn(false);
            Thread.Sleep(150);
        }

        if (_useMainSub)
            TryBandSwap(context);

        var setup = SetupVfosPolicy.Evaluate(
            context.EffectiveDownlinkMode,
            settings.DopplerThresholdFmHz,
            settings.DopplerThresholdLinearHz);
        _thresholdHz = setup.ThresholdHz;
        _interactive = setup.Interactive;

        // FT-847 can revert to narrow FM when SAT frequencies/CTCSS are programmed after mode.
        var deferModeSetup = settings.Type == RigType.YaesuFt847;
        if (!deferModeSetup)
            ConfigureVfoModes(context);

        var rxHz = ToHz(context.Corrected.RadioReceiveKHz);
        var txHz = ToHz(context.Corrected.RadioTransmitKHz);
        WriteInitialFrequencies(settings, rxHz, txHz);
        ApplyCtcss(settings, context, force: true);

        if (deferModeSetup)
            ConfigureVfoModes(context);
        _lastRigRxHz = rxHz;
        _lastRigTxHz = txHz;
        _lastWriteUtc = DateTime.UtcNow;
        _lastPassDownlinkOnVhf = RigSatModeHelper.IsVhfCenterKHz(context.Mode.DownlinkKHz);
        MarkProgrammaticFrequencySettle();
        _suspendDopplerUntilUtc = DateTime.UtcNow.AddMilliseconds(500);
        RestoreOperatorVfo();
    }

    private void TryBandSwap(RigTrackingContext context)
    {
        if (_driver is null)
            return;

        var canExchange = _driver.SupportsVfoExchange;
        var downlinkOnVhf = RigSatModeHelper.IsVhfCenterKHz(context.Mode.DownlinkKHz);
        if (canExchange
            && _lastPassDownlinkOnVhf is bool previousDownlinkOnVhf
            && previousDownlinkOnVhf != downlinkOnVhf)
        {
            _driver.ExchangeVfos();
            return;
        }

        _driver.SelectVfo(RigVfo.Main);
        Thread.Sleep(50);

        var targetRxHz = ToHz(context.Corrected.RadioReceiveKHz);
        var mainHz = _driver.ReadFrequencyHz(RigVfo.Main);
        if (mainHz is > 0)
        {
            if (canExchange)
            {
                if (mainHz.Value > 400_000_000 && targetRxHz < 400_000_000)
                    _driver.ExchangeVfos();
                else if (mainHz.Value < 200_000_000 && targetRxHz > 200_000_000)
                    _driver.ExchangeVfos();
                else if (RigSatModeHelper.NeedsMainSubBandSwap(mainHz.Value, context.Mode.DownlinkKHz))
                    _driver.ExchangeVfos();
            }

            return;
        }

        // CI-V read failed — infer from Sub when downlink/uplink are on opposite bands.
        if (!canExchange || !downlinkOnVhf || !RigSatModeHelper.IsUhfCenterKHz(context.Mode.UplinkKHz))
            return;

        var subHz = _driver.ReadFrequencyHz(RigVfo.Sub);
        if (subHz is > 0 and < 200_000_000)
            _driver.ExchangeVfos();
    }

    private static string PassKey(RigTrackingContext context) =>
        $"{context.TrackState.NoradId}|{context.Mode.Type}|{context.Mode.DownlinkKHz}|{context.Mode.UplinkKHz}";

    private static bool IsIcomSatelliteLayoutRig(RigType type) =>
        type is RigType.IcomIc910 or RigType.IcomIc9100 or RigType.IcomIc9700;

    private void AssignReceiveVfo(RigSettings settings, RigTrackingContext context)
    {
        if (_useMainSub)
            _receiveVfo = RigVfo.Main;
        else if (_isBeaconOnly && IsIcomSatelliteLayoutRig(settings.Type))
            _receiveVfo = RigVfo.Main;
        else
            _receiveVfo = RigVfo.VfoA;
    }

    private void EnsureBeaconDownlinkOnMain(RigTrackingContext context)
    {
        if (_driver is null || _receiveVfo != RigVfo.Main || !_driver.SupportsVfoExchange)
            return;

        _driver.SelectVfo(RigVfo.Main);
        Thread.Sleep(50);
        var mainHz = _driver.ReadFrequencyHz(RigVfo.Main);
        if (mainHz is > 0 && RigSatModeHelper.NeedsMainSubBandSwap(mainHz.Value, context.Mode.DownlinkKHz))
            _driver.ExchangeVfos();
    }

    private void ClearCtcssLeavingSatelliteMode(RigSettings settings)
    {
        if (_driver is null)
            return;

        foreach (var vfo in VfosForSatelliteCtcssClear(settings.Type))
            SetCtcssOffOnVfo(vfo);

        _lastAppliedCtcssHz = null;
        _lastAppliedCtcssSquelch = null;
    }

    private static IEnumerable<RigVfo> VfosForSatelliteCtcssClear(RigType type)
    {
        if (type is RigType.IcomIc910 or RigType.IcomIc9100 or RigType.IcomIc9700
            or RigType.YaesuFt847 or RigType.KenwoodTs2000)
        {
            yield return RigVfo.Main;
            yield return RigVfo.Sub;
            yield break;
        }

        yield return RigVfo.VfoA;
        yield return RigVfo.VfoB;
    }

    private void SetCtcssOffOnVfo(RigVfo vfo)
    {
        if (_driver is null)
            return;

        _driver.SelectVfo(vfo, force: true);
        _driver.SetToneOn(false);
        _driver.SetToneSquelchOn(false);
    }

    private void ConfigureVfoModes(RigTrackingContext context)
    {
        if (_driver is null)
            return;

        if (_isBeaconOnly)
        {
            _driver.SelectVfo(ReceiveVfo());
            _driver.SetMode(context.EffectiveDownlinkMode);
            return;
        }

        if (_useMainSub)
        {
            // Satellite layout: downlink mode on Main, uplink mode on Sub.
            _driver.SelectVfo(RigVfo.Main);
            _driver.SetMode(context.EffectiveDownlinkMode);
            _driver.SelectVfo(RigVfo.Sub);
            _driver.SetMode(context.EffectiveUplinkMode);
            return;
        }

        _driver.SelectVfo(RigVfo.VfoA);
        _driver.SetMode(context.EffectiveDownlinkMode);
        _driver.SelectVfo(RigVfo.VfoB);
        _driver.SetMode(context.EffectiveUplinkMode);
    }

    private void ApplyCtcss(RigSettings settings, RigTrackingContext context, bool force)
    {
        if (_driver is null || context.SelectedCtcssHz is not { } hz || hz <= 0)
            return;

        var squelch = settings.Region == RigRegion.USA;
        if (!force && _lastAppliedCtcssHz == hz && _lastAppliedCtcssSquelch == squelch)
            return;

        // CTCSS on uplink VFO: tone Hz then enable (US: TSQL, EU: repeater tone).
        _driver.SelectVfo(UplinkVfoForCtcss(settings, context), force: true);
        if (squelch)
        {
            _driver.SetToneHz(hz, squelchTone: true);
            _driver.SetToneSquelchOn(true);
        }
        else
        {
            _driver.SetToneHz(hz, squelchTone: false);
            _driver.SetToneOn(true);
        }

        _lastAppliedCtcssHz = hz;
        _lastAppliedCtcssSquelch = squelch;
    }

    private static RigVfo UplinkVfoForCtcss(RigSettings settings, RigTrackingContext context)
    {
        if (settings.Type is RigType.IcomIc910 or RigType.IcomIc9100 or RigType.IcomIc9700 or RigType.YaesuFt847 or RigType.KenwoodTs2000)
            return RigVfo.Sub;

        return RigSatModeHelper.UseMainSubLayout(context.Mode.DownlinkKHz, context.Mode.UplinkKHz)
            ? RigVfo.Sub
            : RigVfo.VfoB;
    }

    private void WriteInitialFrequencies(RigSettings settings, long rxHz, long txHz)
    {
        if (_driver is null)
            return;

        if (_useMainSub)
        {
            // Pass init: clear tone on Main, set RX, then set TX on Sub (CTCSS applied after).
            _driver.SelectVfo(RigVfo.Main);
            if (settings.Region == RigRegion.USA)
                _driver.SetToneSquelchOn(false);
            else
                _driver.SetToneOn(false);
            _driver.SetFrequencyHz(rxHz);
            if (!_isBeaconOnly)
            {
                _driver.SelectVfo(RigVfo.Sub, force: true);
                _driver.SetFrequencyHz(txHz);
            }
        }
        else
        {
            _driver.SelectVfo(ReceiveVfo());
            _driver.SetFrequencyHz(rxHz);
            if (!_isBeaconOnly)
            {
                _driver.SelectVfo(RigVfo.VfoB);
                _driver.SetFrequencyHz(txHz);
            }
        }
    }

    private void NoteContextOffsetChange(RigTrackingContext context)
    {
        if (NearlyEqual(context.ReceiveOffsetKHz, _lastContextRxOffsetKHz))
            return;

        _lastContextRxOffsetKHz = context.ReceiveOffsetKHz;
        _forceFrequencyApply = true;
        _blockKnobCapture = true;
        ClearDialHistory();
        MarkProgrammaticFrequencySettle();
    }

    private void MarkProgrammaticFrequencySettle()
    {
        _ignoreDialUntilUtc = DateTime.UtcNow.AddMilliseconds(PostCatWriteDialSettleMs);
        ClearDialHistory();
    }

    private int EffectiveLinearThresholdHz(RigSettings settings, RigTrackingContext context)
    {
        if (_thresholdHz == 0)
            return 0;

        if (!settings.AdaptiveDopplerThresholdLinear
            || !SetupVfosPolicy.IsLinearMode(context.Mode.DownlinkMode))
            return _thresholdHz;

        var look = context.TrackState.LookAngles!;
        var probe = context.TrackState.RangeRateProbeKmPerSec ?? look.RangeRateKmPerSec;
        var rate = DopplerFrequencyCalculator.EstimateCombinedDopplerRateHzPerSec(
            context.Mode,
            look.RangeRateKmPerSec,
            probe,
            context.ReceiveOffsetKHz,
            _passbandDownlinkAdjustKHz,
            _passbandUplinkAdjustKHz);

        return DopplerFrequencyCalculator.AdaptiveLinearThresholdHz(_thresholdHz, rate);
    }

    private bool ShouldWrite(int thresholdHz, long rxHz, long txHz)
    {
        var rxDelta = Math.Abs(rxHz - _lastRigRxHz);
        var txDelta = Math.Abs(txHz - _lastRigTxHz);
        if (thresholdHz == 0)
            return rxDelta > 0 || txDelta > 0;

        return rxDelta > thresholdHz || txDelta > thresholdHz;
    }

    private bool TryReadReceiveDialHz(out long hz)
    {
        hz = 0;
        if (_driver is null)
            return false;

        var dial = _driver.ReadFrequencyHz(ReceiveVfo());
        if (dial is null or <= 0)
            return false;

        if (!RigFrequencyBands.IsPlausibleReceiveRead(_lastRigRxHz, dial.Value))
            return false;

        hz = dial.Value;
        return true;
    }

    private RigVfo ReceiveVfo() => _receiveVfo;

    private RigVfo TransmitVfo() => _useMainSub ? RigVfo.Sub : RigVfo.VfoB;

    private void RestoreOperatorVfo()
    {
        if (_driver is null)
            return;

        _driver.SelectVfo(ReceiveVfo());
    }

    private bool CanWrite(RigSettings settings) =>
        (DateTime.UtcNow - _lastWriteUtc).TotalMilliseconds >= settings.CatDelayMs;

    private void WriteRx(long hz)
    {
        if (_driver is null)
            return;

        _driver.SelectVfo(ReceiveVfo());
        if (_driver.SetFrequencyHz(hz))
            _lastRigRxHz = hz;
    }

    private void WriteTx(long hz)
    {
        if (_driver is null)
            return;

        _driver.SelectVfo(TransmitVfo());
        if (_driver.SetFrequencyHz(hz))
            _lastRigTxHz = hz;
    }

    private static bool NearlyEqual(double a, double b) => Math.Abs(a - b) < 0.0001;

    private void SyncDisplayFrequencies(RigTrackingContext context)
    {
        if (context.TrackState.LookAngles is null)
            return;

        var corrected = ComputeDoppler(_cachedSettings!, context, usePredictive: true);
        _displayRxHz = ToHz(corrected.RadioReceiveKHz);
        _displayTxHz = ToHz(corrected.RadioTransmitKHz);
    }

    private CorrectedFrequencies ComputeDoppler(
        RigSettings settings,
        RigTrackingContext context,
        bool usePredictive)
    {
        var look = context.TrackState.LookAngles!;
        return DopplerFrequencyCalculator.Compute(
            context.Mode,
            look.RangeRateKmPerSec,
            context.ReceiveOffsetKHz,
            _passbandDownlinkAdjustKHz,
            _passbandUplinkAdjustKHz,
            usePredictive ? DopplerOptionsFor(settings, context) : null);
    }

    private static DopplerComputeOptions? DopplerOptionsFor(RigSettings settings, RigTrackingContext context)
    {
        if (!settings.PredictiveDopplerLinear
            || !SetupVfosPolicy.IsLinearMode(context.Mode.DownlinkMode)
            || context.TrackState.RangeRateProbeKmPerSec is not { } probe)
            return null;

        return new DopplerComputeOptions(PredictiveLinear: true, RangeRateProbeKmPerSec: probe);
    }

    private static long ToHz(double kHz) => (long)Math.Round(kHz * 1000.0);

    private enum RigCommandKind
    {
        PublishContext,
        UpdateSynchronously,
        RunTrackingLoopOnce,
        ApplySelectedCtcss,
        Disconnect,
        Drain,
        Shutdown
    }

    private sealed class RigCommand
    {
        public RigCommand(
            RigCommandKind kind,
            RigSettings? settings = null,
            RigTrackingContext? context = null,
            bool reinitializePass = false)
        {
            Kind = kind;
            Settings = settings ?? new RigSettings();
            Context = context;
            ReinitializePass = reinitializePass;
        }

        public RigCommandKind Kind { get; }
        public RigSettings Settings { get; }
        public RigTrackingContext? Context { get; }
        public bool ReinitializePass { get; }
        public ManualResetEventSlim? Completed { get; set; }
    }
}
