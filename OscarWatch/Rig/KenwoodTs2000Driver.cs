using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using Serilog;

namespace OscarWatch.Rig;

/// <summary>
/// Kenwood TS-2000 CAT driver for cross-band satellite (SATL) doppler tracking.
/// Enter SATL on the radio before tracking; disable TRACE so PC Doppler is not overridden.
/// </summary>
public sealed class KenwoodTs2000Driver : IRigDriver
{
    private static readonly ILogger Log = Serilog.Log.ForContext<KenwoodTs2000Driver>();

    private readonly IKenwoodCatTransport _transport;
    private readonly int _catDelayMs;
    private bool _satelliteMode;
    private RigVfo _currentVfo = RigVfo.Main;
    private long _lastMainHz;
    private long _lastSubHz;
    private long _lastVfoAHz;
    private long _lastVfoBHz;

    public KenwoodTs2000Driver(string port, int baudRate, int catDelayMs = 50)
        : this(new KenwoodCatTransport(port, baudRate), catDelayMs)
    {
    }

    internal KenwoodTs2000Driver(IKenwoodCatTransport transport, int catDelayMs = 50)
    {
        _transport = transport;
        _catDelayMs = catDelayMs;
    }

    public RigType RigType => RigType.KenwoodTs2000;
    public bool IsConnected => _transport.IsOpen;
    public bool SupportsTracking => true;
    public bool SupportsVfoExchange => false;

    public void Open()
    {
        _transport.Open();
        _transport.SendCommand(KenwoodCatCodec.BuildAutoinfoOffCommand(), _catDelayMs);
    }

    public long? ReadFrequencyHz(RigVfo vfo)
    {
        var cached = CachedFrequencyHz(vfo);
        if (!_transport.IsOpen)
            return cached > 0 ? cached : null;

        var letter = VfoLetterFor(vfo);
        var reply = _transport.Transact(KenwoodCatCodec.BuildReadFrequencyCommand(letter), _catDelayMs);
        if (reply is null)
            return cached > 0 ? cached : null;

        if (!KenwoodCatCodec.TryParseFrequencyHz(reply, out var hz) || hz <= 0)
            return cached > 0 ? cached : null;

        StoreFrequencyHz(vfo, hz);
        return hz;
    }

    public bool SetFrequencyHz(long hz)
    {
        if (hz < 0)
            return false;

        StoreFrequencyHz(_currentVfo, hz);
        if (!_transport.IsOpen)
            return true;

        var letter = VfoLetterFor(_currentVfo);
        var cmd = KenwoodCatCodec.BuildSetFrequencyCommand(letter, hz);
        return _transport.SendCommand(cmd, _catDelayMs);
    }

    public void SelectVfo(RigVfo vfo, bool force = false)
    {
        if (_currentVfo == vfo && !force)
            return;

        _currentVfo = vfo;
        if (_satelliteMode || !_transport.IsOpen)
            return;

        var vfoB = vfo is RigVfo.Sub or RigVfo.VfoB;
        _transport.SendCommand(KenwoodCatCodec.BuildSelectVfoCommand(vfoB), _catDelayMs);
    }

    public void SetMode(string mode)
    {
        if (!_transport.IsOpen)
            return;

        if (!KenwoodCatCodec.TryGetModeCode(mode, out var modeCode))
        {
            Log.Warning("TS-2000 unsupported mode {Mode}", mode);
            return;
        }

        if (_satelliteMode)
            SelectControlForCurrentVfo();

        _transport.SendCommand(KenwoodCatCodec.BuildSetModeCommand(modeCode), _catDelayMs);

        if (_satelliteMode)
            _transport.SendCommand(KenwoodCatCodec.BuildControlMainCommand(), _catDelayMs);
    }

    public void SetSplitOn(bool on)
    {
        if (!_transport.IsOpen || _satelliteMode)
            return;

        if (on)
        {
            _transport.SendCommand("FR0;FT1;", _catDelayMs);
        }
        else
        {
            _transport.SendCommand("FR0;FT0;", _catDelayMs);
        }
    }

    public void SetSatelliteMode(bool on)
    {
        _satelliteMode = on;
        if (!_transport.IsOpen || !on)
            return;

        var reply = _transport.Transact(KenwoodCatCodec.BuildSatelliteStatusQuery(), _catDelayMs);
        if (reply is not null && !KenwoodCatCodec.TryParseSatelliteOn(reply))
        {
            Log.Warning(
                "TS-2000 is not in SATL (satellite) mode — press SATL on the radio before tracking. Disable TRACE for PC Doppler.");
        }
    }

    public void ExchangeVfos()
    {
        // TS-2000 band swap in SATL is manual (A/B, TF-SET).
    }

    public void SetToneOn(bool on) => SetCtcssPath(on, squelchTone: false);

    public void SetToneSquelchOn(bool on) => SetCtcssPath(on, squelchTone: true);

    public void SetToneHz(double hz, bool squelchTone)
    {
        if (!_transport.IsOpen)
            return;

        if (!KenwoodCatCodec.TryGetCtcssIndex(hz, out var index))
        {
            Log.Warning("TS-2000 does not support CTCSS {Hz} Hz", hz);
            return;
        }

        _transport.SendCommand(KenwoodCatCodec.BuildCtcssFrequencyCommand(index), _catDelayMs);
    }

    public void Dispose() => _transport.Dispose();

    private void SetCtcssPath(bool on, bool squelchTone)
    {
        if (!_transport.IsOpen)
            return;

        if (squelchTone)
        {
            _transport.SendCommand(KenwoodCatCodec.BuildCtcssEnableCommand(on), _catDelayMs);
            return;
        }

        // USA uplink path: CTCSS encode (CT); tone burst uses TO.
        _transport.SendCommand(KenwoodCatCodec.BuildCtcssEnableCommand(on), _catDelayMs);
        if (!on)
            _transport.SendCommand(KenwoodCatCodec.BuildToneEnableCommand(false), _catDelayMs);
    }

    private void SelectControlForCurrentVfo()
    {
        var sub = _currentVfo is RigVfo.Sub or RigVfo.VfoB;
        _transport.SendCommand(
            sub ? KenwoodCatCodec.BuildControlSubCommand() : KenwoodCatCodec.BuildControlMainCommand(),
            _catDelayMs);
    }

    private long CachedFrequencyHz(RigVfo vfo) => vfo switch
    {
        RigVfo.Main => _lastMainHz,
        RigVfo.Sub => _lastSubHz,
        RigVfo.VfoA => _lastVfoAHz,
        RigVfo.VfoB => _lastVfoBHz,
        _ => 0
    };

    private static char VfoLetterFor(RigVfo vfo) => vfo switch
    {
        RigVfo.Sub or RigVfo.VfoB => 'B',
        _ => 'A'
    };

    private void StoreFrequencyHz(RigVfo vfo, long hz)
    {
        switch (vfo)
        {
            case RigVfo.Main:
                _lastMainHz = hz;
                break;
            case RigVfo.Sub:
                _lastSubHz = hz;
                break;
            case RigVfo.VfoA:
                _lastVfoAHz = hz;
                break;
            case RigVfo.VfoB:
                _lastVfoBHz = hz;
                break;
        }
    }
}
