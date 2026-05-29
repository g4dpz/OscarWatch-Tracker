using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using Serilog;

namespace OscarWatch.Rig;

/// <summary>
/// Kenwood TS-2000 CAT driver for cross-band satellite (SATL) doppler tracking.
/// SATL is enabled via <c>SA</c> on pass start; TRACE is turned off in that command.
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
    public bool SupportsVfoExchange => true;

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

        WithControlForCurrentVfo(() =>
            _transport.SendCommand(KenwoodCatCodec.BuildSetModeCommand(modeCode), _catDelayMs));
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
        if (!_transport.IsOpen)
            return;

        if (on)
        {
            _transport.SendCommand(KenwoodCatCodec.BuildSetSatelliteModeOnCommand(), _catDelayMs);
            var reply = _transport.Transact(KenwoodCatCodec.BuildSatelliteStatusQuery(), _catDelayMs);
            if (reply is null || !KenwoodCatCodec.TryParseSatelliteOn(reply))
            {
                Log.Warning(
                    "TS-2000 did not confirm SATL (satellite) mode after SA command — check CAT and close any radio menu.");
            }

            return;
        }

        _transport.SendCommand(KenwoodCatCodec.BuildSetSatelliteModeOffCommand(), _catDelayMs);
    }

    public void ExchangeVfos()
    {
        if (!_satelliteMode || !_transport.IsOpen)
            return;

        var downlinkHz = ReadFrequencyHz(RigVfo.Main);
        var uplinkHz = ReadFrequencyHz(RigVfo.Sub);
        if (downlinkHz is null or <= 0 || uplinkHz is null or <= 0)
            return;

        _transport.SendCommand(KenwoodCatCodec.BuildSetFrequencyCommand('A', uplinkHz.Value), _catDelayMs);
        _transport.SendCommand(KenwoodCatCodec.BuildSetFrequencyCommand('B', downlinkHz.Value), _catDelayMs);
        (_lastMainHz, _lastSubHz) = (_lastSubHz, _lastMainHz);
        (_lastVfoAHz, _lastVfoBHz) = (_lastVfoBHz, _lastVfoAHz);
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

        var cmd = squelchTone
            ? KenwoodCatCodec.BuildCtcssFrequencyCommand(index)
            : KenwoodCatCodec.BuildToneFrequencyCommand(index);

        WithControlForCurrentVfo(() => _transport.SendCommand(cmd, _catDelayMs));
    }

    public void Dispose() => _transport.Dispose();

    private void SetCtcssPath(bool on, bool squelchTone)
    {
        if (!_transport.IsOpen)
            return;

        var cmd = squelchTone
            ? KenwoodCatCodec.BuildCtcssEnableCommand(on)
            : KenwoodCatCodec.BuildToneEnableCommand(on);

        WithControlForCurrentVfo(() => _transport.SendCommand(cmd, _catDelayMs));
    }

    /// <summary>
    /// Tone/CTCSS CAT applies to the CTRL receiver; in SATL select Main/Sub via DC before MD/TN/CN/TO/CT.
    /// </summary>
    private void WithControlForCurrentVfo(Action action)
    {
        if (_satelliteMode)
            SelectControlForCurrentVfo();

        action();

        if (_satelliteMode)
            _transport.SendCommand(KenwoodCatCodec.BuildControlMainCommand(), _catDelayMs);
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
