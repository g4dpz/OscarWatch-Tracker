using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using Serilog;

namespace OscarWatch.Rig;

/// <summary>
/// Kenwood TH-D74 / TH-D75 CAT driver for dual-radio satellite operation.
/// OscarWatch drives Band B as one physical endpoint; split and VFO exchange are intentionally unused.
/// </summary>
public sealed class KenwoodThD7xDriver : IRigDriver
{
    private static readonly ILogger Log = Serilog.Log.ForContext<KenwoodThD7xDriver>();
    private readonly RigType _rigType;
    private readonly IKenwoodHtTransport _transport;
    private readonly int _catDelayMs;
    private bool _sessionReady;
    private bool _fineTuning;
    private string _mode = "FM";
    private int _lastFrequencyBand = -1;
    private long _lastFrequencyHz;

    public KenwoodThD7xDriver(RigType rigType, string port, int baudRate, int catDelayMs = 50)
        : this(rigType, new KenwoodHtTransport(port, baudRate), catDelayMs)
    {
    }

    internal KenwoodThD7xDriver(RigType rigType, IKenwoodHtTransport transport, int catDelayMs = 50)
    {
        if (rigType is not (RigType.KenwoodThD74 or RigType.KenwoodThD75))
            throw new ArgumentOutOfRangeException(nameof(rigType));
        _rigType = rigType;
        _transport = transport;
        _catDelayMs = catDelayMs;
    }

    public bool IsConnected => _transport.IsOpen;
    public RigType RigType => _rigType;
    public bool SupportsTracking => true;
    public bool SupportsVfoExchange => false;

    public void Open()
    {
        _transport.Open();
        _sessionReady = false;
        _lastFrequencyBand = -1;

        // Do not report a successful connection merely because macOS allowed
        // the /dev/cu.* device to be opened. Put Band B into the expected CAT
        // state and require a valid FO response from the radio. The larger
        // startup delay gives USB CDC devices a full transaction budget.
        if (!EnsureSession())
            throw new InvalidOperationException($"{_rigType} opened, but CAT session setup failed.");

        var response = _transport.Transact(
            KenwoodThD7xCatCodec.BuildReadFrequencyCommand(),
            Math.Max(600, _catDelayMs));

        if (response is null || !KenwoodThD7xCatCodec.TryParseFrequencyHz(response, out var hz))
            throw new InvalidOperationException(
                $"{_rigType} serial port opened, but the radio did not return a valid FO 1 response. " +
                "On macOS select the /dev/cu.* device for the radio and verify PC command mode is enabled.");

        _lastFrequencyHz = hz;
        Log.Information("Connected to {RigType}; initial Band B frequency {FrequencyHz} Hz", _rigType, hz);
    }

    public long? ReadFrequencyHz(RigVfo vfo)
    {
        if (!_transport.IsOpen)
            return _lastFrequencyHz > 0 ? _lastFrequencyHz : null;
        var response = _transport.Transact(KenwoodThD7xCatCodec.BuildReadFrequencyCommand(), _catDelayMs);
        if (response is not null && KenwoodThD7xCatCodec.TryParseFrequencyHz(response, out var hz))
        {
            _lastFrequencyHz = hz;
            return hz;
        }
        return _lastFrequencyHz > 0 ? _lastFrequencyHz : null;
    }

    public bool SetFrequencyHz(long hz)
    {
        if (hz <= 0)
            return false;
        var rounded = KenwoodThD7xCatCodec.RoundFrequencyToStep(hz, _fineTuning);
        _lastFrequencyHz = rounded;
        if (!_transport.IsOpen)
            return true;
        if (!EnsureSession())
            return false;
        if (!_transport.SendCommand(KenwoodThD7xCatCodec.BuildSetFrequencyCommand(rounded), _catDelayMs))
            return false;

        var band = FrequencyBand(rounded);
        if (band != _lastFrequencyBand)
        {
            _lastFrequencyBand = band;
            // TH-D7x stores tuning step per band; a cross-band FQ can discard FT/FS state.
            if (!ApplyTuningStep())
                return false;
        }
        return true;
    }

    public void SelectVfo(RigVfo vfo, bool force = false)
    {
        // A dual-radio endpoint has one OscarWatch VFO. The physical radio is pinned to Band B.
    }

    public void SetMode(string mode)
    {
        _mode = string.IsNullOrWhiteSpace(mode) ? "USB" : mode;
        _fineTuning = KenwoodThD7xCatCodec.UsesFineTuning(_mode);
        if (!_transport.IsOpen || !EnsureSession())
            return;
        if (!_transport.SendCommand(KenwoodThD7xCatCodec.BuildSetModeCommand(_mode), _catDelayMs))
        {
            Log.Warning("{RigType} rejected CAT mode {Mode}", _rigType, _mode);
            return;
        }
        ApplyTuningStep();
    }

    public void SetSplitOn(bool on) { }
    public void SetSatelliteMode(bool on) { }
    public void ExchangeVfos() { }

    // TH-D7x CTCSS programming is not included in CardSat's bench-validated command subset.
    // Leave tone state under operator control rather than emitting unverified CAT writes.
    public void SetToneOn(bool on) { }
    public void SetToneSquelchOn(bool on) { }
    public void SetToneHz(double hz, bool squelchTone) { }

    public void Dispose() => _transport.Dispose();

    private bool EnsureSession()
    {
        if (_sessionReady)
            return true;
        if (!_transport.SendCommand(KenwoodThD7xCatCodec.BuildVfoModeCommand(), 20))
            return false;
        if (!_transport.SendCommand(KenwoodThD7xCatCodec.BuildControlBandCommand(), Math.Max(80, _catDelayMs)))
            return false;
        _sessionReady = true;
        return true;
    }

    private bool ApplyTuningStep()
    {
        if (!_transport.SendCommand(KenwoodThD7xCatCodec.BuildFineTuneCommand(_fineTuning), 20))
            return false;
        return !_fineTuning || _transport.SendCommand(KenwoodThD7xCatCodec.BuildFineStepCommand(), 20);
    }

    private static int FrequencyBand(long hz) => hz < 30_000_000 ? 0 : hz < 300_000_000 ? 1 : 2;
}
