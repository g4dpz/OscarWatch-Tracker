using System.IO.Ports;

namespace OscarWatch.Rotator;

internal sealed class SerialRotatorTransport : IRotatorSerialTransport
{
    private readonly SerialPort _port;

    public SerialRotatorTransport(
        string portName,
        int baudRate,
        int readTimeoutMs,
        int writeTimeoutMs,
        string newLine,
        bool dtrEnable = false,
        bool rtsEnable = false)
    {
        _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            ReadTimeout = readTimeoutMs,
            WriteTimeout = writeTimeoutMs,
            NewLine = newLine,
            DtrEnable = dtrEnable,
            RtsEnable = rtsEnable
        };
    }

    public bool IsOpen => _port.IsOpen;

    public bool DtrEnable
    {
        get => _port.DtrEnable;
        set => _port.DtrEnable = value;
    }

    public bool RtsEnable
    {
        get => _port.RtsEnable;
        set => _port.RtsEnable = value;
    }

    public void Open() => _port.Open();

    public void Write(string text) => _port.Write(text);

    public void Write(byte[] buffer, int offset, int count) => _port.Write(buffer, offset, count);

    public void DiscardInBuffer() => _port.DiscardInBuffer();

    public void DiscardOutBuffer() => _port.DiscardOutBuffer();

    public string ReadLine() => _port.ReadLine();

    public string ReadExisting() => _port.ReadExisting();

    public int Read(byte[] buffer, int offset, int count) => _port.Read(buffer, offset, count);

    public void Dispose() => _port.Dispose();
}
