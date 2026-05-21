using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using OscarWatch.Core.Services;

namespace OscarWatch.Rig;

public sealed class RigController : IRigController, IDisposable
{
    private IRigDriver? _driver;
    private string? _connectedKey;
    private string? _passKey;
    private long _lastRigRxHz;
    private long _lastRigTxHz;
    private long _displayRxHz;
    private long _displayTxHz;
    private readonly Func<RigSettings, IRigDriver>? _driverFactory;
    private DateTime _lastWriteUtc = DateTime.MinValue;
    private int _thresholdHz;
    private bool _interactive;
    private bool _useMainSub;
    private bool _isBeaconOnly;
    private readonly Queue<long> _dialHistory = new();
    private double _manualRxAdjustKHz;
    private double _manualTxAdjustKHz;
    private string? _statusMessage;
    private bool _isTracking;
    private bool _catUpdatesPaused;
    private bool _passInitPending;
    private double? _lastAppliedCtcssHz;
    private bool? _lastAppliedCtcssSquelch;

    public RigController(Func<RigSettings, IRigDriver>? driverFactory = null) =>
        _driverFactory = driverFactory;

    public RigConnectionStatus GetStatus() => new(
        _driver?.IsConnected == true,
        _isTracking,
        _statusMessage,
        DisplayHz(_displayRxHz, _lastRigRxHz),
        DisplayHz(_displayTxHz, _lastRigTxHz),
        _catUpdatesPaused);

    private static long? DisplayHz(long displayHz, long lastWrittenHz) =>
        displayHz > 0 ? displayHz : lastWrittenHz > 0 ? lastWrittenHz : null;

    public void Update(RigSettings settings, RigTrackingContext? context)
    {
        if (!settings.Enabled || settings.Type == RigType.None)
        {
            Disconnect();
            _statusMessage = null;
            _isTracking = false;
            return;
        }

        if (settings.Type == RigType.IcomIc9700)
        {
            if (EnsureConnected(settings) && context is not null)
                SyncDisplayFrequencies(context);
            _statusMessage = "IC-9700 tracking not yet implemented";
            _isTracking = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.Port) && settings.Type != RigType.Dummy)
        {
            Disconnect();
            _statusMessage = "No COM port selected";
            _isTracking = false;
            return;
        }

        if (!EnsureConnected(settings))
        {
            _statusMessage = "Rig not connected";
            _isTracking = false;
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

        if (context.TrackState.LookAngles.ElevationDeg < settings.TrackStartElevationDeg)
        {
            _isTracking = false;
            _statusMessage = "Connected (below track elevation)";
            TryApplyCtcssIfChanged(settings, context);
            return;
        }

        if (_driver is null || !_driver.SupportsTracking)
            return;

        _catUpdatesPaused = settings.CatUpdatesPaused;
        _isBeaconOnly = context.Mode.IsBeaconOnly;
        var newPassKey = $"{context.TrackState.NoradId}|{context.Mode.Type}";
        if (!string.Equals(_passKey, newPassKey, StringComparison.Ordinal))
        {
            _passKey = newPassKey;
            _manualRxAdjustKHz = 0;
            _manualTxAdjustKHz = 0;
            _dialHistory.Clear();
            _lastAppliedCtcssHz = null;
            _lastAppliedCtcssSquelch = null;
            if (settings.CatUpdatesPaused)
                _passInitPending = true;
            else
                RunPassInit(settings, context);
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

        TryApplyCtcssIfChanged(settings, context);

        _isTracking = true;
        ProcessTrackingTick(settings, context);
        _statusMessage = "Tracking";
    }

    public void Disconnect()
    {
        _driver?.Dispose();
        _driver = null;
        _connectedKey = null;
        _passKey = null;
        _lastRigRxHz = 0;
        _lastRigTxHz = 0;
        _displayRxHz = 0;
        _displayTxHz = 0;
        _isTracking = false;
        _dialHistory.Clear();
        _passInitPending = false;
        _catUpdatesPaused = false;
        _lastAppliedCtcssHz = null;
        _lastAppliedCtcssSquelch = null;
    }

    public void Dispose() => Disconnect();

    private bool EnsureConnected(RigSettings settings)
    {
        var key = $"{settings.Type}|{settings.Port}|{settings.BaudRate}|{settings.CivAddress}";
        if (_driver is not null && _connectedKey == key && _driver.IsConnected)
            return true;

        Disconnect();
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
        TryApplyCtcssIfChanged(settings, context);

        var rxHz = ToHz(context.Corrected.RadioReceiveKHz);
        var txHz = ToHz(context.Corrected.RadioTransmitKHz);
        WriteInitialFrequencies(rxHz, txHz);
        _lastRigRxHz = rxHz;
        _lastRigTxHz = txHz;
        _lastWriteUtc = DateTime.UtcNow;
    }

    private void TryBandSwap(RigTrackingContext context)
    {
        if (_driver is null)
            return;

        _driver.SelectVfo(RigVfo.Main);
        var current = _driver.GetFrequencyHz();
        if (current is null or <= 0)
            return;

        var targetRx = ToHz(context.Corrected.RadioReceiveKHz);
        if (current > 400_000_000 && targetRx < 400_000_000)
            _driver.ExchangeVfos();
        else if (current < 200_000_000 && targetRx > 200_000_000)
            _driver.ExchangeVfos();
    }

    private void ConfigureVfoModes(RigTrackingContext context)
    {
        if (_driver is null)
            return;

        _driver.SelectVfo(_useMainSub ? RigVfo.Main : RigVfo.VfoA);
        _driver.SetMode(context.Mode.DownlinkMode);
        _driver.SelectVfo(_useMainSub ? RigVfo.Sub : RigVfo.VfoB);
        _driver.SetMode(context.Mode.UplinkMode);
    }

    private void TryApplyCtcssIfChanged(RigSettings settings, RigTrackingContext context)
    {
        if (_driver is null || context.SelectedCtcssHz is not { } hz || hz <= 0)
            return;

        var squelch = settings.Region == RigRegion.USA;
        if (_lastAppliedCtcssHz == hz && _lastAppliedCtcssSquelch == squelch)
            return;

        ApplyCtcss(settings, context);
        _lastAppliedCtcssHz = hz;
        _lastAppliedCtcssSquelch = squelch;
    }

    private void ApplyCtcss(RigSettings settings, RigTrackingContext context)
    {
        if (_driver is null || context.SelectedCtcssHz is not { } hz || hz <= 0)
            return;

        // Uplink CTCSS always on Sub for Icom satellite rigs (CI-V 0x07 0xD1), not VFO B.
        _driver.SelectVfo(UplinkVfoForCtcss(settings, context));
        var squelch = settings.Region == RigRegion.USA;
        _driver.SetToneHz(hz, squelch);
        if (squelch)
            _driver.SetToneSquelchOn(true);
        else
            _driver.SetToneOn(true);
    }

    /// <summary>Sub on IC-910/9700 satellite passes; VFO B only for non-Icom split fallback.</summary>
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

    private void ProcessTrackingTick(RigSettings settings, RigTrackingContext context)
    {
        if (_driver is null)
            return;

        if (_interactive && SetupVfosPolicy.IsLinearMode(context.Mode.DownlinkMode))
            ProcessInteractiveDial(context);

        var corrected = DopplerFrequencyCalculator.Compute(
            context.Mode,
            context.TrackState.LookAngles!.RangeRateKmPerSec,
            context.TransmitOffsetKHz + _manualTxAdjustKHz,
            context.ReceiveOffsetKHz + _manualRxAdjustKHz);

        var rxHz = ToHz(corrected.RadioReceiveKHz);
        var txHz = ToHz(corrected.RadioTransmitKHz);

        if (!CanWrite(settings))
            return;

        if (!ShouldWrite(rxHz, txHz))
            return;

        if (Math.Abs(rxHz - _lastRigRxHz) > _thresholdHz || _thresholdHz == 0)
            WriteRx(rxHz);

        if (!_isBeaconOnly && (Math.Abs(txHz - _lastRigTxHz) > _thresholdHz || _thresholdHz == 0))
            WriteTx(txHz);

        _lastWriteUtc = DateTime.UtcNow;
    }

    private bool ShouldWrite(long rxHz, long txHz)
    {
        var rxDelta = Math.Abs(rxHz - _lastRigRxHz);
        var txDelta = _isBeaconOnly ? 0 : Math.Abs(txHz - _lastRigTxHz);
        if (_thresholdHz == 0)
            return rxDelta > 0 || txDelta > 0;

        // Either leg past threshold (RX offset, TX offset, or doppler on that leg).
        return rxDelta > _thresholdHz || txDelta > _thresholdHz;
    }

    private void ProcessInteractiveDial(RigTrackingContext context)
    {
        if (_driver is null)
            return;

        _driver.SelectVfo(_useMainSub ? RigVfo.Main : RigVfo.VfoA);
        var dial = _driver.GetFrequencyHz();
        if (dial is null or <= 0)
            return;

        _dialHistory.Enqueue(dial.Value);
        while (_dialHistory.Count > 4)
            _dialHistory.Dequeue();

        if (_dialHistory.Count < 4)
            return;

        var first = _dialHistory.Peek();
        if (_dialHistory.Any(f => f != first))
            return;

        if (Math.Abs(dial.Value - _lastRigRxHz) <= 1)
            return;

        var deltaKhz = (dial.Value - _lastRigRxHz) / 1000.0;
        if (context.Mode.DopplerCorrection == DopplerCorrection.Reverse)
        {
            _manualTxAdjustKHz -= deltaKhz;
            _manualRxAdjustKHz += deltaKhz;
        }
        else
        {
            _manualTxAdjustKHz += deltaKhz;
            _manualRxAdjustKHz += deltaKhz;
        }

        _lastRigRxHz = dial.Value;
    }

    private bool CanWrite(RigSettings settings) =>
        (DateTime.UtcNow - _lastWriteUtc).TotalMilliseconds >= settings.CatDelayMs;

    private void WriteRx(long hz)
    {
        if (_driver is null)
            return;
        _driver.SelectVfo(_useMainSub ? RigVfo.Main : RigVfo.VfoA);
        if (_driver.SetFrequencyHz(hz))
            _lastRigRxHz = hz;
    }

    private void WriteTx(long hz)
    {
        if (_driver is null)
            return;
        _driver.SelectVfo(_useMainSub ? RigVfo.Sub : RigVfo.VfoB);
        if (_driver.SetFrequencyHz(hz))
            _lastRigTxHz = hz;
    }

    private void SyncDisplayFrequencies(RigTrackingContext context)
    {
        if (context.TrackState.LookAngles is null)
            return;

        var corrected = DopplerFrequencyCalculator.Compute(
            context.Mode,
            context.TrackState.LookAngles.RangeRateKmPerSec,
            context.TransmitOffsetKHz + _manualTxAdjustKHz,
            context.ReceiveOffsetKHz + _manualRxAdjustKHz);
        _displayRxHz = ToHz(corrected.RadioReceiveKHz);
        _displayTxHz = ToHz(corrected.RadioTransmitKHz);
    }

    private static long ToHz(double kHz) => (long)Math.Round(kHz * 1000.0);
}
