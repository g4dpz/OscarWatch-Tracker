using System.IO.Ports;
using OscarWatch.Core.Radio;
using Serilog;

namespace OscarWatch.Rig;

internal sealed class IcomSerialTransport : IDisposable
{
    private static readonly ILogger Log = Serilog.Log.ForContext<IcomSerialTransport>();
    private readonly SerialPort _port;
    private readonly int _civAddress;

    public IcomSerialTransport(string portName, int baudRate, int civAddress)
    {
        _civAddress = civAddress;
        _port = new SerialPort(portName, baudRate)
        {
            ReadTimeout = 1000,
            WriteTimeout = 1000,
            DtrEnable = false,
            RtsEnable = false
        };
    }

    public bool IsOpen => _port.IsOpen;

    public void Open()
    {
        if (!_port.IsOpen)
            _port.Open();
    }

    public byte[] WriteCommand(ReadOnlySpan<byte> body, bool retry = false)
    {
        if (!_port.IsOpen)
            return [];

        var frame = IcomCivCodec.BuildCommandFrame(_civAddress, body);
        try
        {
            _port.DiscardInBuffer();
            _port.Write(frame, 0, frame.Length);
            Thread.Sleep(50);
            return ReadResponse();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "CI-V write failed on {PortName}", _port.PortName);
            return [];
        }
    }

    private byte[] ReadResponse()
    {
        var buffer = new List<byte>();
        try
        {
            var b = _port.ReadByte();
            if (b < 0)
                return [];
            buffer.Add((byte)b);
            while (_port.BytesToRead > 0)
            {
                b = _port.ReadByte();
                if (b >= 0)
                    buffer.Add((byte)b);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "CI-V read failed on {PortName}", _port.PortName);
            return [];
        }

        var data = buffer.ToArray();
        while (CountFd(data) > 1)
        {
            var idx = Array.IndexOf(data, (byte)0xFD);
            if (idx < 0)
                break;
            data = data[(idx + 1)..];
        }

        return data;
    }

    private static int CountFd(byte[] data)
    {
        var count = 0;
        foreach (var b in data)
            if (b == 0xFD)
                count++;
        return count;
    }

    public void Dispose()
    {
        if (_port.IsOpen)
            _port.Close();
        _port.Dispose();
    }
}
