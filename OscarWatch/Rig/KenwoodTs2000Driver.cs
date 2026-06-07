using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using Serilog;

namespace OscarWatch.Rig;

/// <summary>
/// Kenwood TS-2000 CAT driver for cross-band satellite (SATL) doppler tracking.
/// FA/FB for RX/TX frequencies; SA for main/sub CTRL (no DC/FR in SATL).
/// </summary>
public sealed class KenwoodTs2000Driver : IRigDriver
{
    private static readonly ILogger Log = Serilog.Log.ForContext<KenwoodTs2000Driver>();

    private readonly IKenwoodCatTransport _transport;
    private readonly int _catDelayMs;
    private readonly int _satModeSettlingDelayMs;
    private readonly int _satModeRetryCount;
    private readonly int _satModeRetryDelayMs;
    private bool _satelliteMode;
    private bool _satelliteLayoutConfirmed;
    private RigVfo _currentVfo = RigVfo.Main;
    private long _lastMainHz;
    private long _lastSubHz;
    private long _lastVfoAHz;
    private long _lastVfoBHz;

    public KenwoodTs2000Driver(string port, int baudRate, int catDelayMs = 50, int satModeSettlingDelayMs = 250, int satModeRetryCount = 3, int satModeRetryDelayMs = 200)
        : this(new KenwoodCatTransport(port, baudRate), catDelayMs, satModeSettlingDelayMs, satModeRetryCount, satModeRetryDelayMs)
    {
    }

    internal KenwoodTs2000Driver(IKenwoodCatTransport transport, int catDelayMs = 50, int satModeSettlingDelayMs = 250, int satModeRetryCount = 3, int satModeRetryDelayMs = 200)
    {
        _transport = transport;
        _catDelayMs = catDelayMs;
        _satModeSettlingDelayMs = satModeSettlingDelayMs;
        _satModeRetryCount = satModeRetryCount;
        _satModeRetryDelayMs = satModeRetryDelayMs;
    }

    public RigType RigType => RigType.KenwoodTs2000;
    public bool IsConnected => _transport.IsOpen;
    public bool SupportsTracking => true;
    public bool IsSatelliteModeActive => _satelliteMode;
    public bool SupportsVfoExchange => true;

    public void Open()
    {
        _transport.Open();
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

        if (_satelliteMode)
            return false;

        var letter = VfoLetterFor(_currentVfo);
        return _transport.SendFireAndForget(KenwoodCatCodec.BuildSetFrequencyCommand(letter, hz), _catDelayMs);
    }

    /// <summary>
    /// SATL doppler update: FA/FB/SM cluster plus FA; link-hold polls. Call once per RX/TX batch.
    /// </summary>
    public bool ApplySatelliteDopplerStep(long downlinkHz, long uplinkHz)
    {
        if (!_satelliteMode || !_transport.IsOpen || downlinkHz <= 0 || uplinkHz <= 0)
            return false;

        _lastMainHz = downlinkHz;
        _lastSubHz = uplinkHz;
        _lastVfoAHz = downlinkHz;
        _lastVfoBHz = uplinkHz;

        var vhfSm = KenwoodCatCodec.BuildSatelliteBandSelectSubCommand(downlinkHz);

        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetFrequencyCommand('A', downlinkHz), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetFrequencyCommand('B', uplinkHz), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSatelliteBandSelectMainCommand(), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetFrequencyCommand('A', downlinkHz), _catDelayMs);
        _transport.SendFireAndForget(vhfSm, _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetFrequencyCommand('B', uplinkHz), _catDelayMs);
        _transport.SendFireAndForget(vhfSm, _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSatelliteBandSelectMainCommand(), _catDelayMs);

        SendSatelliteLinkHoldPolls();
        return true;
    }

    /// <summary>
    /// Programs pass frequencies after SAT entry: double FA/FB, SM, main/sub finalize (modes, PC050, tones).
    /// </summary>
    public void ApplySatellitePassFrequencies(
        long downlinkHz,
        long uplinkHz,
        char downlinkModeCode,
        char uplinkModeCode)
    {
        if (!_satelliteMode || !_transport.IsOpen)
            return;

        _lastMainHz = downlinkHz;
        _lastSubHz = uplinkHz;
        _lastVfoAHz = downlinkHz;
        _lastVfoBHz = uplinkHz;

        ProgramSatelliteFrequencies(downlinkHz, uplinkHz);
        FinalizeSatelliteMainPath(downlinkModeCode, downlinkHz);
        FinalizeSatelliteSubPath(uplinkModeCode, downlinkHz, uplinkHz);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetSatelliteModeOnCommand(), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildAutoinfoOffCommand(), _catDelayMs);

        SendSatelliteLinkHoldPolls();
    }

    public void SelectVfo(RigVfo vfo, bool force = false)
    {
        if (_currentVfo == vfo && !force)
            return;

        _currentVfo = vfo;
        if (_satelliteMode || !_transport.IsOpen)
            return;

        var vfoB = vfo is RigVfo.Sub or RigVfo.VfoB;
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSelectVfoCommand(vfoB), _catDelayMs);
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
        {
            var sub = _currentVfo is RigVfo.Sub or RigVfo.VfoB;
            if (sub)
            {
                _transport.SendFireAndForget(KenwoodCatCodec.BuildSetSatelliteModeOnSubControlCommand(), _catDelayMs);
                _transport.SendFireAndForget(KenwoodCatCodec.BuildSetModeCommand(modeCode), _catDelayMs);
                _transport.SendFireAndForget(KenwoodCatCodec.BuildSetSatelliteModeOnCommand(), _catDelayMs);
            }
            else
            {
                _transport.SendFireAndForget(KenwoodCatCodec.BuildSetSatelliteModeOnCommand(), _catDelayMs);
                _transport.SendFireAndForget(KenwoodCatCodec.BuildSetModeCommand(modeCode), _catDelayMs);
            }

            return;
        }

        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetModeCommand(modeCode), _catDelayMs);
    }

    public void SetSplitOn(bool on)
    {
        if (!_transport.IsOpen || _satelliteMode)
            return;

        if (on)
            _transport.SendFireAndForget("FR0;FT1;", _catDelayMs);
        else
            _transport.SendFireAndForget("FR0;FT0;", _catDelayMs);
    }

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
            _satelliteLayoutConfirmed = false;
            return;
        }

        if (on)
        {
            var result = TryEnableSatelliteMode();
            _satelliteMode = result;
            _satelliteLayoutConfirmed = result;
            if (!result)
            {
                Log.Error(
                    "TS-2000 SATL not confirmed after {RetryCount} SA; verification attempts. Radio may not be in satellite mode.",
                    _satModeRetryCount);
            }

            return;
        }

        _satelliteMode = false;
        _satelliteLayoutConfirmed = false;
        SendSatelliteModeExitSequence();
    }

    private void SendSatelliteModeExitSequence()
    {
        foreach (var cmd in KenwoodCatCodec.SatelliteModeExitSequence)
        {
            if (KenwoodCatCodec.IsSatelliteModeExitReadCommand(cmd))
                _transport.Transact(cmd, _catDelayMs);
            else
                _transport.SendFireAndForget(cmd, _catDelayMs);
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

        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetFrequencyCommand('A', uplinkHz.Value), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetFrequencyCommand('B', downlinkHz.Value), _catDelayMs);
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

        if (_satelliteMode)
        {
            var sub = _currentVfo is RigVfo.Sub or RigVfo.VfoB;
            if (sub)
            {
                _transport.SendFireAndForget(KenwoodCatCodec.BuildSetSatelliteModeOnSubControlCommand(), _catDelayMs);
                _transport.SendFireAndForget(cmd, _catDelayMs);
                _transport.SendFireAndForget(KenwoodCatCodec.BuildSetSatelliteModeOnCommand(), _catDelayMs);
            }
            else
            {
                _transport.SendFireAndForget(KenwoodCatCodec.BuildSetSatelliteModeOnCommand(), _catDelayMs);
                _transport.SendFireAndForget(cmd, _catDelayMs);
            }

            return;
        }

        _transport.SendFireAndForget(cmd, _catDelayMs);
    }

    public void Dispose() => _transport.Dispose();

    private void SetCtcssPath(bool on, bool squelchTone)
    {
        if (!_transport.IsOpen)
            return;

        var cmd = squelchTone
            ? KenwoodCatCodec.BuildCtcssEnableCommand(on)
            : KenwoodCatCodec.BuildToneEnableCommand(on);

        if (_satelliteMode)
        {
            var sub = _currentVfo is RigVfo.Sub or RigVfo.VfoB;
            if (sub)
            {
                _transport.SendFireAndForget(KenwoodCatCodec.BuildSetSatelliteModeOnSubControlCommand(), _catDelayMs);
                _transport.SendFireAndForget(cmd, _catDelayMs);
                _transport.SendFireAndForget(KenwoodCatCodec.BuildSetSatelliteModeOnCommand(), _catDelayMs);
            }
            else
            {
                _transport.SendFireAndForget(KenwoodCatCodec.BuildSetSatelliteModeOnCommand(), _catDelayMs);
                _transport.SendFireAndForget(cmd, _catDelayMs);
            }

            return;
        }

        _transport.SendFireAndForget(cmd, _catDelayMs);
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
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetSatelliteModeOnCommand(), _catDelayMs);

        // Settling delay: give the radio time to transition its internal state to SATL
        // before proceeding with the entry handshake and verification query.
        if (_satModeSettlingDelayMs > 0)
            Thread.Sleep(_satModeSettlingDelayMs);

        foreach (var toneOff in KenwoodCatCodec.SatelliteModeEntryToneOffSequence)
            _transport.SendFireAndForget(toneOff, _catDelayMs);

        SendSatelliteEntryHandshake();

        // Retry loop: attempt SA; verification up to _satModeRetryCount times
        // with inter-attempt delays, accounting for variable radio processing time.
        for (var attempt = 1; attempt <= _satModeRetryCount; attempt++)
        {
            var reply = _transport.Transact(KenwoodCatCodec.BuildSatelliteStatusQuery(), _catDelayMs);
            if (reply is not null && KenwoodCatCodec.TryParseSatelliteOn(reply))
            {
                SendSatelliteToneAndSquelchOff();
                return true;
            }

            if (attempt < _satModeRetryCount && _satModeRetryDelayMs > 0)
                Thread.Sleep(_satModeRetryDelayMs);
        }

        return false;
    }

    private void SendSatelliteEntryHandshake()
    {
        _transport.Transact(KenwoodCatCodec.BuildReadFrequencyCommand('A'), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSatelliteEntryTsCommand(), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildAutoinfoExtendedCommand(), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetSatelliteModeOnCommand(), _catDelayMs);

        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetSatelliteModeOnCommand(), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetModeCommand('2'), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetSatelliteModeOnSubControlCommand(), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetModeCommand('1'), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetSatelliteModeOnCommand(), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildToneEnableCommand(false), _catDelayMs);
    }

    private void ProgramSatelliteFrequencies(long downlinkHz, long uplinkHz)
    {
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetFrequencyCommand('A', downlinkHz), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetFrequencyCommand('B', uplinkHz), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetFrequencyCommand('A', downlinkHz), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetFrequencyCommand('B', uplinkHz), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSatelliteBandSelectMainCommand(), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSatelliteBandSelectSubCommand(downlinkHz), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetSatelliteModeOnCommand(), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildToneEnableCommand(false), _catDelayMs);
    }

    private void FinalizeSatelliteMainPath(char downlinkModeCode, long downlinkHz)
    {
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetSatelliteModeOnCommand(), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildToneEnableCommand(false), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildCtcssEnableCommand(false), _catDelayMs);
        _transport.SendFireAndForget("DQ0;", _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetSatelliteModeOnCommand(), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetModeCommand(downlinkModeCode), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSatellitePowerLevelCommand(), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSatelliteBandSelectSubCommand(downlinkHz), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSatelliteBandSelectMainCommand(), _catDelayMs);
    }

    private void FinalizeSatelliteSubPath(char uplinkModeCode, long downlinkHz, long uplinkHz)
    {
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetSatelliteModeOnSubControlCommand(), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetModeCommand(uplinkModeCode), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSatelliteBandSelectSubCommand(downlinkHz), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildToneEnableCommand(false), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildCtcssEnableCommand(false), _catDelayMs);
        _transport.SendFireAndForget("DQ0;", _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSatellitePowerLevelCommand(), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSatelliteBandSelectMainCommand(), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSatelliteBandSelectSubCommand(uplinkHz), _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetSatelliteModeOnCommand(), _catDelayMs);
    }

    private void SendSatelliteToneAndSquelchOff()
    {
        ClearSatelliteTonePath(subControl: false);
        ClearSatelliteTonePath(subControl: true);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildSetSatelliteModeOnCommand(), _catDelayMs);
    }

    private void ClearSatelliteTonePath(bool subControl)
    {
        if (subControl)
            _transport.SendFireAndForget(KenwoodCatCodec.BuildSetSatelliteModeOnSubControlCommand(), _catDelayMs);
        else
            _transport.SendFireAndForget(KenwoodCatCodec.BuildSetSatelliteModeOnCommand(), _catDelayMs);

        _transport.SendFireAndForget(KenwoodCatCodec.BuildToneEnableCommand(false), _catDelayMs);
        _transport.SendFireAndForget("DQ0;", _catDelayMs);
        _transport.SendFireAndForget(KenwoodCatCodec.BuildCtcssEnableCommand(false), _catDelayMs);
    }
}
