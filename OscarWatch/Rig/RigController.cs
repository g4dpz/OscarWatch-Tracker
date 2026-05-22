using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using OscarWatch.Core.Services;

namespace OscarWatch.Rig;

public sealed class RigController : IRigController, IDisposable
{
    private const int DialHistoryLength = 4;
    private const int FmCompanionLegHz = 10;
    private static readonly TimeSpan LoopInterval = TimeSpan.FromMilliseconds(150);

    private readonly object _sync = new();
    private readonly Func<RigSettings, IRigDriver>? _driverFactory;
    private readonly long[] _rxDialHistory = new long[DialHistoryLength];

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
    private int _rxDialHistoryCount;
    private bool _vfoNotMoving;
    private bool _vfoNotMovingPrevious;
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
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;

    public RigController(Func<RigSettings, IRigDriver>? driverFactory = null) =>
        _driverFactory = driverFactory;

    public RigConnectionStatus GetStatus()
    {
        lock (_sync)
        {
            return new RigConnectionStatus(
                _driver?.IsConnected == true,
                _isTracking,
                _statusMessage,
                DisplayHz(_displayRxHz, _lastRigRxHz),
                DisplayHz(_displayTxHz, _lastRigTxHz),
                _catUpdatesPaused,
                _passbandDownlinkAdjustKHz,
                _passbandUplinkAdjustKHz);
        }
    }

    /// <summary>UI thread: refresh cached pass/settings (1 Hz). CAT runs on the doppler loop.</summary>
    public void PublishContext(RigSettings settings, RigTrackingContext? context)
    {
        lock (_sync)
        {
            _cachedSettings = settings;
            _cachedContext = context;
            ApplyPublishState(settings, context);
        }

        if (settings.Enabled && settings.Type != RigType.None)
            EnsureLoopRunning();
        else
            StopLoop();
    }

    /// <summary>Runs one doppler iteration (tests and legacy <see cref="Update"/>).</summary>
    public void RunTrackingLoopOnce()
    {
        lock (_sync)
            RunLoopIteration(ignoreDopplerSuspend: true);
    }

    public void Update(RigSettings settings, RigTrackingContext? context)
    {
        PublishContext(settings, context);
        RunTrackingLoopOnce();
    }

    public void ApplySelectedCtcss(RigSettings settings, RigTrackingContext? context)
    {
        lock (_sync)
        {
            if (!settings.Enabled || settings.Type == RigType.None || context is null)
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
    }

    public void Disconnect()
    {
        StopLoop();
        lock (_sync)
        {
            _driver?.Dispose();
            _driver = null;
            _connectedKey = null;
            _passKey = null;
            _cachedContext = null;
            ResetTrackingState();
        }
    }

    public void Dispose() => Disconnect();

    private void ApplyPublishState(RigSettings settings, RigTrackingContext? context)
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

        if (context is not null)
            SyncDisplayFrequencies(context);

        if (context is null || context.TrackState.LookAngles is null)
        {
            _isTracking = false;
            _statusMessage = "Connected";
            return;
        }

        _catUpdatesPaused = settings.CatUpdatesPaused;
        _isBeaconOnly = context.Mode.IsBeaconOnly;

        if (context.TrackState.LookAngles.ElevationDeg < settings.TrackStartElevationDeg)
        {
            _isTracking = false;
            _statusMessage = "Connected (below track elevation)";
            return;
        }

        if (_driver is null || !_driver.SupportsTracking)
            return;

        var newPassKey = PassKey(context);
        if (!string.Equals(_passKey, newPassKey, StringComparison.Ordinal))
        {
            BeginNewPass(settings, context, newPassKey);
        }

        if (settings.CatUpdatesPaused)
        {
            _isTracking = false;
            _statusMessage = "CAT paused (manual tuning)";
            return;
        }

        if (_passInitPending)
        {
            RunPassInit(settings, context);
            _passInitPending = false;
        }
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
        _suspendDopplerUntilUtc = DateTime.MinValue;
    }

    private void EnsureLoopRunning()
    {
        if (_loopTask is { IsCompleted: false })
            return;

        _loopCts = new CancellationTokenSource();
        _loopTask = Task.Run(() => LoopAsync(_loopCts.Token));
    }

    private void StopLoop()
    {
        if (_loopCts is null)
            return;

        _loopCts.Cancel();
        try
        {
            _loopTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // cancelled
        }

        _loopCts.Dispose();
        _loopCts = null;
        _loopTask = null;
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(LoopInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            lock (_sync)
                RunLoopIteration(ignoreDopplerSuspend: false);
        }
    }

    private void RunLoopIteration(bool ignoreDopplerSuspend = false)
    {
        if (_driver is null || !_driver.IsConnected || _cachedContext is null)
            return;

        if (!_cachedSettings.Enabled || _cachedSettings.CatUpdatesPaused || !_isTracking)
            return;

        if (!ignoreDopplerSuspend && DateTime.UtcNow < _suspendDopplerUntilUtc)
            return;

        if (_cachedContext.TrackState.LookAngles is null
            || _cachedContext.TrackState.LookAngles.ElevationDeg < _cachedSettings.TrackStartElevationDeg)
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

        if (!_forceFrequencyApply && (!_vfoNotMoving || !_vfoNotMovingPrevious))
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
            // QTrig REV: Main dial up → downlink nominal up, uplink nominal down.
            newDown = _passbandDownlinkAdjustKHz + deltaKhz;
            newUp = _passbandUplinkAdjustKHz - deltaKhz;
        }
        else
        {
            // QTrig NOR: both nominals move with Main dial.
            newDown = _passbandDownlinkAdjustKHz + deltaKhz;
            newUp = _passbandUplinkAdjustKHz + deltaKhz;
        }

        if (NearlyEqual(newDown, _passbandDownlinkAdjustKHz) && NearlyEqual(newUp, _passbandUplinkAdjustKHz))
            return;

        _passbandDownlinkAdjustKHz = newDown;
        _passbandUplinkAdjustKHz = newUp;
        SeedDialHistoryStable(dialHz);
        _vfoNotMoving = true;
        _vfoNotMovingPrevious = true;
    }

    private void SeedDialHistoryStable(long dialHz)
    {
        for (var i = 0; i < DialHistoryLength; i++)
            _rxDialHistory[i] = dialHz;

        _rxDialHistoryCount = DialHistoryLength;
    }

    private int KnobTuneThresholdHz() => Math.Max(_thresholdHz, 100);

    private void SampleReceiveDial()
    {
        _vfoNotMovingPrevious = _vfoNotMoving;

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
            && _rxDialHistory[1] == _rxDialHistory[2]
            && _rxDialHistory[2] == _rxDialHistory[3];
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
        _vfoNotMovingPrevious = false;
        Array.Clear(_rxDialHistory);
    }

    private void WriteDopplerFrequencies(RigSettings settings, RigTrackingContext context)
    {
        var corrected = DopplerFrequencyCalculator.Compute(
            context.Mode,
            context.TrackState.LookAngles!.RangeRateKmPerSec,
            context.ReceiveOffsetKHz,
            _passbandDownlinkAdjustKHz,
            _passbandUplinkAdjustKHz);

        SyncDisplayFrequencies(context);

        var rxHz = ToHz(corrected.RadioReceiveKHz);
        var txHz = ToHz(corrected.RadioTransmitKHz);

        if (!CanWrite(settings))
            return;

        var forceApply = _forceFrequencyApply;
        _forceFrequencyApply = false;
        if (!forceApply && !ShouldWrite(rxHz, txHz))
            return;

        var rxDelta = Math.Abs(rxHz - _lastRigRxHz);
        var txDelta = _isBeaconOnly ? 0 : Math.Abs(txHz - _lastRigTxHz);
        var writeRx = forceApply || rxDelta > _thresholdHz || _thresholdHz == 0;
        var writeTx = !_isBeaconOnly && (forceApply || txDelta > _thresholdHz || _thresholdHz == 0);

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
        catch
        {
            _driver?.Dispose();
            _driver = null;
            return false;
        }
    }

    private void RunPassInit(RigSettings settings, RigTrackingContext context)
    {
        if (_driver is null)
            return;

        _useMainSub = RigSatModeHelper.UseMainSubLayout(context.Mode.DownlinkKHz, context.Mode.UplinkKHz);

        if (!_useMainSub && !_isBeaconOnly)
        {
            _driver.SetSatelliteMode(false);
            _driver.SetSplitOn(true);
        }
        else if (!_useMainSub && _isBeaconOnly)
        {
            _driver.SetSatelliteMode(false);
            _driver.SetSplitOn(false);
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
            context.Mode.DownlinkMode,
            settings.DopplerThresholdFmHz,
            settings.DopplerThresholdLinearHz);
        _thresholdHz = setup.ThresholdHz;
        _interactive = setup.Interactive;

        ConfigureVfoModes(context);
        ApplyCtcss(settings, context, force: true);

        var rxHz = ToHz(context.Corrected.RadioReceiveKHz);
        var txHz = ToHz(context.Corrected.RadioTransmitKHz);
        WriteInitialFrequencies(rxHz, txHz);
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

        var downlinkOnVhf = RigSatModeHelper.IsVhfCenterKHz(context.Mode.DownlinkKHz);
        if (_lastPassDownlinkOnVhf is bool previousDownlinkOnVhf && previousDownlinkOnVhf != downlinkOnVhf)
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
            if (mainHz.Value > 400_000_000 && targetRxHz < 400_000_000)
                _driver.ExchangeVfos();
            else if (mainHz.Value < 200_000_000 && targetRxHz > 200_000_000)
                _driver.ExchangeVfos();
            else if (RigSatModeHelper.NeedsMainSubBandSwap(mainHz.Value, context.Mode.DownlinkKHz))
                _driver.ExchangeVfos();
            return;
        }

        // CI-V read failed — infer from Sub when downlink/uplink are on opposite bands.
        if (!downlinkOnVhf || !RigSatModeHelper.IsUhfCenterKHz(context.Mode.UplinkKHz))
            return;

        var subHz = _driver.ReadFrequencyHz(RigVfo.Sub);
        if (subHz is > 0 and < 200_000_000)
            _driver.ExchangeVfos();
    }

    private static string PassKey(RigTrackingContext context) =>
        $"{context.TrackState.NoradId}|{context.Mode.Type}|{context.Mode.DownlinkKHz}|{context.Mode.UplinkKHz}";

    private void ConfigureVfoModes(RigTrackingContext context)
    {
        if (_driver is null)
            return;

        _driver.SelectVfo(_useMainSub ? RigVfo.Main : RigVfo.VfoA);
        _driver.SetMode(context.Mode.DownlinkMode);
        _driver.SelectVfo(_useMainSub ? RigVfo.Sub : RigVfo.VfoB);
        _driver.SetMode(context.Mode.UplinkMode);
    }

    private void ApplyCtcss(RigSettings settings, RigTrackingContext context, bool force)
    {
        if (_driver is null || context.SelectedCtcssHz is not { } hz || hz <= 0)
            return;

        var squelch = settings.Region == RigRegion.USA;
        if (!force && _lastAppliedCtcssHz == hz && _lastAppliedCtcssSquelch == squelch)
            return;

        _driver.SelectVfo(UplinkVfoForCtcss(settings, context));
        _driver.SetToneHz(hz, squelch);
        if (squelch)
            _driver.SetToneSquelchOn(true);
        else
            _driver.SetToneOn(true);

        _lastAppliedCtcssHz = hz;
        _lastAppliedCtcssSquelch = squelch;
    }

    private static RigVfo UplinkVfoForCtcss(RigSettings settings, RigTrackingContext context)
    {
        if (settings.Type is RigType.IcomIc910 or RigType.IcomIc9700)
            return RigVfo.Sub;

        return RigSatModeHelper.UseMainSubLayout(context.Mode.DownlinkKHz, context.Mode.UplinkKHz)
            ? RigVfo.Sub
            : RigVfo.VfoB;
    }

    private void WriteInitialFrequencies(long rxHz, long txHz)
    {
        if (_driver is null)
            return;

        if (_useMainSub)
        {
            _driver.SelectVfo(RigVfo.Main);
            _driver.SetFrequencyHz(rxHz);
            if (!_isBeaconOnly)
            {
                _driver.SelectVfo(RigVfo.Sub);
                _driver.SetFrequencyHz(txHz);
            }
        }
        else
        {
            _driver.SelectVfo(RigVfo.VfoA);
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
        _ignoreDialUntilUtc = DateTime.UtcNow.AddMilliseconds(600);
        ClearDialHistory();
    }

    private bool ShouldWrite(long rxHz, long txHz)
    {
        var rxDelta = Math.Abs(rxHz - _lastRigRxHz);
        var txDelta = _isBeaconOnly ? 0 : Math.Abs(txHz - _lastRigTxHz);
        if (_thresholdHz == 0)
            return rxDelta > 0 || txDelta > 0;

        return rxDelta > _thresholdHz || txDelta > _thresholdHz;
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

    private RigVfo ReceiveVfo() => _useMainSub ? RigVfo.Main : RigVfo.VfoA;

    private RigVfo TransmitVfo() => _useMainSub ? RigVfo.Sub : RigVfo.VfoB;

    private void RestoreOperatorVfo()
    {
        if (_driver is null || _isBeaconOnly)
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

        var corrected = DopplerFrequencyCalculator.Compute(
            context.Mode,
            context.TrackState.LookAngles.RangeRateKmPerSec,
            context.ReceiveOffsetKHz,
            _passbandDownlinkAdjustKHz,
            _passbandUplinkAdjustKHz);
        _displayRxHz = ToHz(corrected.RadioReceiveKHz);
        _displayTxHz = ToHz(corrected.RadioTransmitKHz);
    }

    private static long ToHz(double kHz) => (long)Math.Round(kHz * 1000.0);
}
