using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using Serilog;

namespace OscarWatch.Rig;

/// <summary>
/// Yaesu FT-991 / FT-991A CAT driver for dual-radio endpoints (VFO-A only per physical radio).
/// </summary>
public class YaesuFt991Driver : IRigDriver
{
    private static readonly ILogger Log = Serilog.Log.ForContext<YaesuFt991Driver>();

    private readonly IYaesuNewCatTransport _transport;
    private readonly RigType _rigType;
    private readonly int _catDelayMs;
    private RigVfo _currentVfo = RigVfo.Main;
    private int? _lastCtcssIndex;
    private long _lastMainHz;
    private long _lastSubHz;
    private long _lastVfoAHz;
    private long _lastVfoBHz;

    public YaesuFt991Driver(
        RigType rigType,
        string port,
        int baudRate,
        RigRegion region = RigRegion.EU,
        int catDelayMs = 50)
        : this(rigType, new YaesuNewCatTransport(port, baudRate), region, catDelayMs)
    {
    }

    internal YaesuFt991Driver(
        RigType rigType,
        IYaesuNewCatTransport transport,
        RigRegion region = RigRegion.EU,
        int catDelayMs = 50)
    {
        if (!RigSettings.IsYaesuNewCatDualEndpoint(rigType))
            throw new ArgumentOutOfRangeException(nameof(rigType));

        _rigType = rigType;
        _transport = transport;
        _catDelayMs = catDelayMs;
    }

    public RigType RigType => _rigType;
    public bool IsConnected => _transport.IsOpen;
    public bool SupportsTracking => true;
    public bool SupportsVfoExchange => false;

    public void Open()
    {
        _transport.Open();
        SetDialLock(false);
    }

    public long? ReadFrequencyHz(RigVfo vfo)
    {
        var cached = CachedFrequencyHz(vfo);
        if (!_transport.IsOpen)
            return cached > 0 ? cached : null;

        var reply = _transport.Transact(YaesuFt991CatCodec.BuildReadFrequencyCommand(UseVfoB(vfo)), _catDelayMs);
        if (reply is null)
            return cached > 0 ? cached : null;

        if (!YaesuFt991CatCodec.TryParseFrequencyHz(reply, out var hz) || hz <= 0)
            return cached > 0 ? cached : null;

        StoreFrequencyHz(vfo, hz);
        return hz;
    }

    public bool SetFrequencyHz(long hz)
    {
        StoreFrequencyHz(_currentVfo, hz);
        if (!_transport.IsOpen)
            return true;

        try
        {
            var cmd = YaesuFt991CatCodec.BuildSetFrequencyCommand(UseVfoB(_currentVfo), hz);
            return _transport.SendCommand(cmd, _catDelayMs);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Log.Warning(ex, "{RigType} frequency {Hz} out of CAT range", _rigType, hz);
            return false;
        }
    }

    public void SelectVfo(RigVfo vfo, bool force = false)
    {
        if (!force && _currentVfo == vfo)
            return;

        _currentVfo = vfo;
    }

    public void SetMode(string mode)
    {
        if (!_transport.IsOpen)
            return;

        if (!YaesuFt991CatCodec.TryGetModeCode(mode, out var code))
        {
            Log.Warning("{RigType} unsupported mode {Mode}", _rigType, mode);
            return;
        }

        _transport.SendCommand(YaesuFt991CatCodec.BuildSetModeCommand(code), _catDelayMs);
        SetDialLock(YaesuFt991CatCodec.IsFmMode(mode));
    }

    public void SetSatelliteMode(bool on)
    {
    }

    public void SetSplitOn(bool on)
    {
    }

    public void ExchangeVfos()
    {
    }

    public void SetToneOn(bool on)
    {
        if (!_transport.IsOpen)
            return;

        if (!on)
        {
            _transport.SendCommand(YaesuFt991CatCodec.BuildCtcssOffCommand(), _catDelayMs);
            return;
        }

        if (_lastCtcssIndex is not { } index)
            return;

        _transport.SendCommand(YaesuFt991CatCodec.BuildCtcssEncodeCommand(index), _catDelayMs);
    }

    public void SetToneSquelchOn(bool on)
    {
        if (!_transport.IsOpen)
            return;

        if (!on)
        {
            _transport.SendCommand(YaesuFt991CatCodec.BuildCtcssOffCommand(), _catDelayMs);
            return;
        }

        if (_lastCtcssIndex is not { } index)
            return;

        _transport.SendCommand(YaesuFt991CatCodec.BuildCtcssSquelchCommand(index), _catDelayMs);
    }

    public void SetToneHz(double hz, bool squelchTone)
    {
        if (!_transport.IsOpen)
            return;

        if (!YaesuFt991CatCodec.TryGetCtcssIndex(hz, out var index))
        {
            Log.Warning("{RigType} does not support CTCSS {Hz} Hz", _rigType, hz);
            return;
        }

        _lastCtcssIndex = index;
        var cmd = squelchTone
            ? YaesuFt991CatCodec.BuildCtcssSquelchCommand(index)
            : YaesuFt991CatCodec.BuildCtcssEncodeCommand(index);
        _transport.SendCommand(cmd, _catDelayMs);
    }

    public void Dispose()
    {
        try
        {
            if (_transport.IsOpen)
                SetDialLock(false);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "{RigType} dial unlock failed", _rigType);
        }

        _transport.Dispose();
    }

    private void SetDialLock(bool on) =>
        _transport.SendCommand(YaesuFt991CatCodec.BuildDialLockCommand(on), _catDelayMs);

    private static bool UseVfoB(RigVfo vfo) =>
        vfo is RigVfo.Sub or RigVfo.VfoB;

    private long CachedFrequencyHz(RigVfo vfo) => vfo switch
    {
        RigVfo.Main => _lastMainHz,
        RigVfo.Sub => _lastSubHz,
        RigVfo.VfoA => _lastVfoAHz,
        RigVfo.VfoB => _lastVfoBHz,
        _ => 0
    };

    private void StoreFrequencyHz(RigVfo vfo, long hz)
    {
        switch (vfo)
        {
            case RigVfo.Main:
            case RigVfo.VfoA:
                _lastMainHz = hz;
                _lastVfoAHz = hz;
                break;
            case RigVfo.Sub:
            case RigVfo.VfoB:
                _lastSubHz = hz;
                _lastVfoBHz = hz;
                break;
        }
    }
}
