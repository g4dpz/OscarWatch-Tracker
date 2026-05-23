using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using Serilog;

namespace OscarWatch.Rig;

public abstract class IcomCivDriverBase : IRigDriver
{
    private static readonly ILogger Log = Serilog.Log.ForContext<IcomCivDriverBase>();
    private IcomSerialTransport? _transport;
    private RigVfo _currentVfo = RigVfo.VfoA;
    private long _lastMainHz;
    private long _lastSubHz;
    private long _lastVfoAFreq;
    private long _lastVfoBFreq;

    protected IcomCivDriverBase(RigType rigType, string port, int baudRate, string civAddressHex)
    {
        RigType = rigType;
        Port = port;
        BaudRate = baudRate;
        CivAddress = IcomCivCodec.ParseCivAddressHex(civAddressHex);
    }

    public RigType RigType { get; }
    protected string Port { get; }
    protected int BaudRate { get; }
    protected int CivAddress { get; }

    public bool IsConnected => _transport?.IsOpen == true;
    public abstract bool SupportsTracking { get; }

    public void Open()
    {
        _transport?.Dispose();
        _transport = new IcomSerialTransport(Port, BaudRate, CivAddress);
        try
        {
            _transport.Open();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to open CI-V port {Port} at {BaudRate}", Port, BaudRate);
            _transport.Dispose();
            _transport = null;
        }
    }

    public long? ReadFrequencyHz(RigVfo vfo)
    {
        SelectVfoInternal(vfo, force: true);
        var cached = CachedFrequencyHz(vfo);

        if (_transport is null || !IsConnected)
            return cached > 0 ? cached : null;

        Thread.Sleep(50);
        var response = _transport.WriteCommand([0x03]);
        var hz = IcomCivCodec.DecodeFrequencyFromResponse(response);
        if (hz is { } value && IcomCivCodec.IsValidSatelliteFrequencyHz(value))
        {
            StoreFrequencyHz(vfo, value);
            return value;
        }

        return cached > 0 ? cached : null;
    }

    public bool SetFrequencyHz(long hz)
    {
        if (!IcomCivCodec.IsValidSatelliteFrequencyHz(hz))
            return false;

        if (_transport is null || !IsConnected)
        {
            StoreFrequencyHz(_currentVfo, hz);
            return true;
        }

        StoreFrequencyHz(_currentVfo, hz);

        var body = IcomCivCodec.EncodeSetFrequencyHz(hz);
        var response = _transport.WriteCommand(body);
        return response.Length > 0 && response.Contains((byte)0xFB);
    }

    public void SelectVfo(RigVfo vfo, bool force = false) => SelectVfoInternal(vfo, force);

    private void SelectVfoInternal(RigVfo vfo, bool force)
    {
        if (!force && _currentVfo == vfo && _transport is { IsOpen: true })
            return;

        _currentVfo = vfo;
        if (_transport is null || !IsConnected)
            return;

        var cmd = vfo switch
        {
            RigVfo.VfoA => new byte[] { 0x07, 0x00 },
            RigVfo.VfoB => new byte[] { 0x07, 0x01 },
            RigVfo.Main => new byte[] { 0x07, 0xD0 },
            RigVfo.Sub => new byte[] { 0x07, 0xD1 },
            _ => new byte[] { 0x07, 0x00 }
        };
        WriteWithRetry(cmd);
    }

    public void SetMode(string mode)
    {
        if (_transport is null || !IsConnected)
            return;

        var upper = mode.ToUpperInvariant();
        if (upper is "FM" or "FMN")
        {
            WriteWithRetry([0x06, 0x04]);
            WriteWithRetry([0x06, 0x05]);
            return;
        }

        var cmd = upper switch
        {
            "USB" => new byte[] { 0x06, 0x01 },
            "LSB" or "DATA-LSB" => new byte[] { 0x06, 0x00 },
            "CW" => new byte[] { 0x06, 0x03 },
            "DATA-USB" => new byte[] { 0x06, 0x01 },
            _ => null
        };
        if (cmd is not null)
            WriteWithRetry(cmd);
    }

    public void SetSplitOn(bool on) =>
        WriteWithRetry(on ? [0x0F, 0x01] : [0x0F, 0x00]);

    public abstract void SetSatelliteMode(bool on);

    public void ExchangeVfos()
    {
        _currentVfo = _currentVfo switch
        {
            RigVfo.Main => RigVfo.Sub,
            RigVfo.Sub => RigVfo.Main,
            RigVfo.VfoA => RigVfo.VfoB,
            _ => RigVfo.VfoA
        };
        (_lastMainHz, _lastSubHz) = (_lastSubHz, _lastMainHz);
        (_lastVfoAFreq, _lastVfoBFreq) = (_lastVfoBFreq, _lastVfoAFreq);
        // Exchange is a toggle — must be sent once; WriteWithRetry would swap twice and undo it.
        WriteOnce([0x07, 0xB0]);
        Thread.Sleep(150);
    }

    private long CachedFrequencyHz(RigVfo vfo) => vfo switch
    {
        RigVfo.Main => _lastMainHz,
        RigVfo.Sub => _lastSubHz,
        RigVfo.VfoA => _lastVfoAFreq,
        RigVfo.VfoB => _lastVfoBFreq,
        _ => 0
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
                _lastVfoAFreq = hz;
                break;
            case RigVfo.VfoB:
                _lastVfoBFreq = hz;
                break;
        }
    }

    public void SetToneOn(bool on) =>
        _transport?.WriteCommand(on ? [0x16, 0x42, 0x01] : [0x16, 0x42, 0x00]);

    public void SetToneSquelchOn(bool on) =>
        _transport?.WriteCommand(on ? [0x16, 0x43, 0x01] : [0x16, 0x43, 0x00]);

    public void SetToneHz(double hz, bool squelchTone)
    {
        if (_transport is null || !IsConnected)
            return;
        _transport.WriteCommand(IcomCivCodec.EncodeToneHz(hz, squelchTone));
    }

    protected void WriteWithRetry(ReadOnlySpan<byte> body)
    {
        if (_transport is null || !IsConnected)
            return;
        _transport.WriteCommand(body, retry: true);
        Thread.Sleep(100);
        _transport.WriteCommand(body, retry: true);
    }

    private void WriteOnce(ReadOnlySpan<byte> body)
    {
        if (_transport is null || !IsConnected)
            return;
        _transport.WriteCommand(body, retry: true);
    }

    public void Dispose()
    {
        _transport?.Dispose();
        _transport = null;
    }
}
