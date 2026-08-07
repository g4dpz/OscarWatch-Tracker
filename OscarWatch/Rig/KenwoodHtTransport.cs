using System.IO.Ports;
using System.Text;
using Serilog;

namespace OscarWatch.Rig;

/// <summary>TH-D74/TH-D75 PC-command transport: 8N1 ASCII, CR terminated, no RTS/CTS.</summary>
internal sealed class KenwoodHtTransport : IKenwoodHtTransport
{
    private static readonly ILogger Log = Serilog.Log.ForContext<KenwoodHtTransport>();
    private readonly SerialPort _port;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public KenwoodHtTransport(string portName, int baudRate)
    {
        _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            ReadTimeout = 250,
            WriteTimeout = 1000,
            DtrEnable = false,
            RtsEnable = false,
            NewLine = "\r"
        };
    }

    public bool IsOpen => _port.IsOpen;

    public void Open()
    {
        if (!_port.IsOpen)
            _port.Open();
    }

    public bool SendCommand(string command, int postDelayMs = 50)
    {
        if (string.IsNullOrWhiteSpace(command))
            return false;
        _gate.Wait();
        try
        {
            if (!_port.IsOpen)
                return false;
            DrainInput();
            _port.Write(Normalize(command));
            Thread.Sleep(Math.Max(0, postDelayMs));
            if (_port.BytesToRead <= 0)
                return true;
            var response = ReadUntilCr(Math.Max(80, postDelayMs + 80));
            return !IsRejected(response);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Kenwood HT CAT send failed on {Port} for {Command}", _port.PortName, command.Trim());
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    public string? Transact(string command, int postDelayMs = 50)
    {
        if (string.IsNullOrWhiteSpace(command))
            return null;
        _gate.Wait();
        try
        {
            if (!_port.IsOpen)
                return null;
            DrainInput();
            _port.Write(Normalize(command));
            return ReadUntilCr(Math.Max(220, postDelayMs + 170));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Kenwood HT CAT transaction failed on {Port} for {Command}", _port.PortName, command.Trim());
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        try { if (_port.IsOpen) _port.Close(); } catch { }
        _port.Dispose();
        _gate.Dispose();
    }

    private static string Normalize(string command) => command.TrimEnd('\r', '\n') + "\r";

    private void DrainInput()
    {
        try { _port.DiscardInBuffer(); } catch { }
    }

    private string? ReadUntilCr(int timeoutMs)
    {
        var sb = new StringBuilder(96);
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            try
            {
                var c = _port.ReadChar();
                if (c == '\r')
                    return sb.ToString();
                if (c >= 0 && c != '\n')
                    sb.Append((char)c);
            }
            catch (TimeoutException)
            {
                // Continue until our transaction deadline; SerialPort has its own shorter timeout.
            }
        }
        return sb.Length > 0 ? sb.ToString() : null;
    }

    private static bool IsRejected(string? response)
    {
        var text = response?.Trim();
        return string.Equals(text, "N", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "?", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "E", StringComparison.OrdinalIgnoreCase);
    }
}
