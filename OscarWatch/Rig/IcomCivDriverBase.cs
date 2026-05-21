using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;

namespace OscarWatch.Rig;

public abstract class IcomCivDriverBase : IRigDriver
{
    private IcomSerialTransport? _transport;
    private RigVfo _currentVfo = RigVfo.VfoA;
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
        catch
        {
            _transport.Dispose();
            _transport = null;
        }
    }

    public long? GetFrequencyHz()
    {
        if (_transport is null || !IsConnected)
            return _currentVfo is RigVfo.VfoA or RigVfo.Main ? _lastVfoAFreq : _lastVfoBFreq;

        var response = _transport.WriteCommand([0x03]);
        var hz = IcomCivCodec.DecodeFrequencyFromResponse(response);
        return hz is { } value && IcomCivCodec.IsValidSatelliteFrequencyHz(value) ? value : null;
    }

    public bool SetFrequencyHz(long hz)
    {
        if (!IcomCivCodec.IsValidSatelliteFrequencyHz(hz))
            return false;

        if (_transport is null || !IsConnected)
        {
            if (_currentVfo is RigVfo.VfoA or RigVfo.Main)
                _lastVfoAFreq = hz;
            else
                _lastVfoBFreq = hz;
            return true;
        }

        if (_currentVfo is RigVfo.VfoA or RigVfo.Main)
            _lastVfoAFreq = hz;
        else
            _lastVfoBFreq = hz;

        var body = IcomCivCodec.EncodeSetFrequencyHz(hz);
        var response = _transport.WriteCommand(body);
        return response.Length > 0 && response.Contains((byte)0xFB);
    }

    public void SelectVfo(RigVfo vfo)
    {
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
        _currentVfo = _currentVfo is RigVfo.VfoA or RigVfo.Main ? RigVfo.VfoB : RigVfo.VfoA;
        WriteWithRetry([0x07, 0xB0]);
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

    public void Dispose()
    {
        _transport?.Dispose();
        _transport = null;
    }
}
