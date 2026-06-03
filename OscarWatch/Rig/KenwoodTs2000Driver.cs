using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using Serilog;

namespace OscarWatch.Rig;

/// <summary>
/// Kenwood TS-2000 CAT driver for cross-band satellite (SATL) doppler tracking.
/// SATL is enabled via <c>SA1010110;</c> on pass start (SatPC32-compatible), then encode tones cleared with <c>TO0;</c>.
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
    public bool IsSatelliteModeActive => _satelliteMode;
    public bool SupportsVfoExchange => true;

    public void Open()
    {
        _transport.Open();
        // SatPC32 enables AI2 during SAT entry, not AI0 on connect.
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
        if (!_transport.SendCommand(cmd, _catDelayMs))
            return false;

        if (_satelliteMode)
            SendSatelliteBandSelect(letter, hz);

        return true;
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

    /// <summary>
    /// SatPC32 pass tail after downlink/uplink frequencies and modes are programmed (PC050, band SM, tone off).
    /// </summary>
    public void FinalizeSatellitePassSetup(long downlinkHz, long uplinkHz, char downlinkModeCode, char uplinkModeCode)
    {
        if (!_satelliteMode || !_transport.IsOpen)
            return;

        RunSatPc32MainPathFinalize(downlinkModeCode, downlinkHz);
        RunSatPc32SubPathFinalize(uplinkModeCode, downlinkHz, uplinkHz);
        RestoreSatelliteDcLayout();
        SendSatelliteLinkHoldPolls();
    }

    /// <summary>SatPC32-style <c>FA;</c> read polls after a doppler frequency batch.</summary>
    public void SendSatelliteLinkHoldPolls()
    {
        if (!_satelliteMode || !_transport.IsOpen)
            return;

        for (var i = 0; i < KenwoodCatCodec.SatelliteLinkHoldPollCount; i++)
            _transport.Transact(KenwoodCatCodec.BuildReadFrequencyCommand('A'), _catDelayMs);
    }

    public void SetSatelliteMode(bool on)
    {
        if (!_transport.IsOpen)
        {
            _satelliteMode = on;
            return;
        }

        if (on)
        {
            _satelliteMode = TryEnableSatelliteMode();
            if (!_satelliteMode)
            {
                Log.Warning(
                    "TS-2000 did not confirm SATL (satellite) mode after SA command — check CAT and close any radio menu.");
            }

            return;
        }

        _satelliteMode = false;
        SendSatelliteModeExitSequence();
    }

    private void SendSatelliteModeExitSequence()
    {
        foreach (var cmd in KenwoodCatCodec.SatelliteModeExitSequence)
        {
            if (KenwoodCatCodec.IsSatelliteModeExitReadCommand(cmd))
                _transport.Transact(cmd, _catDelayMs);
            else
                _transport.SendCommand(cmd, _catDelayMs);
        }
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
    /// Tone/CTCSS and MD apply to the CTRL band (DC P2); in SATL use DC01/DC00 before TN/CN/MD/TO/CT.
    /// </summary>
    private void WithControlForCurrentVfo(Action action)
    {
        if (_satelliteMode)
            SelectControlBandForCurrentVfo();

        action();

        if (_satelliteMode)
            RestoreSatelliteDcLayout();
    }

    private void RestoreSatelliteDcLayout() =>
        _transport.SendCommand(KenwoodCatCodec.BuildControlMainCommand(), _catDelayMs);

    private void SelectControlBandForCurrentVfo()
    {
        var sub = _currentVfo is RigVfo.Sub or RigVfo.VfoB;
        _transport.SendCommand(
            sub
                ? KenwoodCatCodec.BuildSetSatelliteModeOnSubControlCommand()
                : KenwoodCatCodec.BuildSetSatelliteModeOnCommand(),
            _catDelayMs);
        _transport.SendCommand(
            sub ? KenwoodCatCodec.BuildControlSubCommand() : KenwoodCatCodec.BuildControlMainCommand(),
            _catDelayMs);
    }

    private void SendSatelliteBandSelect(char vfoLetter, long hz)
    {
        _ = vfoLetter;
        _transport.SendCommand(KenwoodCatCodec.BuildSatelliteBandSelectMainCommand(), _catDelayMs);
        _transport.SendCommand(KenwoodCatCodec.BuildSatelliteBandSelectSubCommand(hz), _catDelayMs);
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

    private bool TryEnableSatelliteMode()
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            SendSatPc32SatelliteEntryPreamble();

            var reply = _transport.Transact(KenwoodCatCodec.BuildSatelliteStatusQuery(), _catDelayMs);
            if (reply is not null && KenwoodCatCodec.TryParseSatelliteOn(reply))
            {
                SendSatelliteToneAndSquelchOff();
                return true;
            }
        }

        return false;
    }

    private void SendSatPc32SatelliteEntryPreamble()
    {
        _transport.SendCommand(KenwoodCatCodec.BuildSetSatelliteModeOnCommand(), _catDelayMs);
        foreach (var toneOff in KenwoodCatCodec.SatelliteModeEntryToneOffSequence)
            _transport.SendCommand(toneOff, _catDelayMs);

        _transport.Transact(KenwoodCatCodec.BuildReadFrequencyCommand('A'), _catDelayMs);
        _transport.SendCommand(KenwoodCatCodec.BuildSatelliteEntryTsCommand(), _catDelayMs);
        _transport.SendCommand(KenwoodCatCodec.BuildAutoinfoExtendedCommand(), _catDelayMs);
        _transport.SendCommand(KenwoodCatCodec.BuildSetSatelliteModeOnCommand(), _catDelayMs);

        // SatPC32 programs USB then LSB on main CTRL before pass frequencies.
        SendModeOnMainControl('2');
        _transport.SendCommand(KenwoodCatCodec.BuildSetSatelliteModeOnCommand(), _catDelayMs);
        SendModeOnMainControl('1');
        _transport.SendCommand(KenwoodCatCodec.BuildSetSatelliteModeOnCommand(), _catDelayMs);
        _transport.SendCommand(KenwoodCatCodec.BuildToneEnableCommand(false), _catDelayMs);
    }

    private void RunSatPc32MainPathFinalize(char downlinkModeCode, long downlinkHz)
    {
        _transport.SendCommand(KenwoodCatCodec.BuildSetSatelliteModeOnCommand(), _catDelayMs);
        _transport.SendCommand(KenwoodCatCodec.BuildToneEnableCommand(false), _catDelayMs);
        _transport.SendCommand(KenwoodCatCodec.BuildCtcssEnableCommand(false), _catDelayMs);
        _transport.SendCommand("DQ0;", _catDelayMs);
        SendModeOnMainControl(downlinkModeCode);
        _transport.SendCommand(KenwoodCatCodec.BuildSatellitePowerLevelCommand(), _catDelayMs);
        _transport.SendCommand(KenwoodCatCodec.BuildSatelliteBandSelectSubCommand(downlinkHz), _catDelayMs);
        _transport.SendCommand(KenwoodCatCodec.BuildSatelliteBandSelectMainCommand(), _catDelayMs);
    }

    private void RunSatPc32SubPathFinalize(char uplinkModeCode, long downlinkHz, long uplinkHz)
    {
        _transport.SendCommand(KenwoodCatCodec.BuildSetSatelliteModeOnSubControlCommand(), _catDelayMs);
        _transport.SendCommand(KenwoodCatCodec.BuildControlSubCommand(), _catDelayMs);
        _transport.SendCommand(KenwoodCatCodec.BuildSetModeCommand(uplinkModeCode), _catDelayMs);
        _transport.SendCommand(KenwoodCatCodec.BuildSatelliteBandSelectSubCommand(downlinkHz), _catDelayMs);
        _transport.SendCommand(KenwoodCatCodec.BuildToneEnableCommand(false), _catDelayMs);
        _transport.SendCommand(KenwoodCatCodec.BuildCtcssEnableCommand(false), _catDelayMs);
        _transport.SendCommand("DQ0;", _catDelayMs);
        _transport.SendCommand(KenwoodCatCodec.BuildSatellitePowerLevelCommand(), _catDelayMs);
        _transport.SendCommand(KenwoodCatCodec.BuildSatelliteBandSelectMainCommand(), _catDelayMs);
        _transport.SendCommand(KenwoodCatCodec.BuildSatelliteBandSelectSubCommand(uplinkHz), _catDelayMs);
        _transport.SendCommand(KenwoodCatCodec.BuildSetSatelliteModeOnCommand(), _catDelayMs);
    }

    private void SendModeOnMainControl(char modeCode)
    {
        _transport.SendCommand(KenwoodCatCodec.BuildSetSatelliteModeOnCommand(), _catDelayMs);
        _transport.SendCommand(KenwoodCatCodec.BuildControlMainCommand(), _catDelayMs);
        _transport.SendCommand(KenwoodCatCodec.BuildSetModeCommand(modeCode), _catDelayMs);
    }

    private void SendSatelliteToneAndSquelchOff()
    {
        ClearSatelliteTonePath(subControl: false);
        ClearSatelliteTonePath(subControl: true);
        RestoreSatelliteDcLayout();
    }

    private void ClearSatelliteTonePath(bool subControl)
    {
        _transport.SendCommand(
            subControl
                ? KenwoodCatCodec.BuildSetSatelliteModeOnSubControlCommand()
                : KenwoodCatCodec.BuildSetSatelliteModeOnCommand(),
            _catDelayMs);
        _transport.SendCommand(
            subControl ? KenwoodCatCodec.BuildControlSubCommand() : KenwoodCatCodec.BuildControlMainCommand(),
            _catDelayMs);
        _transport.SendCommand(KenwoodCatCodec.BuildToneEnableCommand(false), _catDelayMs);
        _transport.SendCommand("DQ0;", _catDelayMs);
        _transport.SendCommand(KenwoodCatCodec.BuildCtcssEnableCommand(false), _catDelayMs);
    }
}
