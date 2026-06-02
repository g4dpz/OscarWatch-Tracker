using System.IO.Ports;
using System.Text;
using Serilog;

namespace OscarWatch.Rig;

/// <summary>FT-991 / FT-991A ASCII CAT: 8N2, hardware RTS, semicolon-terminated commands.</summary>
internal sealed class YaesuNewCatTransport : IYaesuNewCatTransport
{
    private static readonly ILogger Log = Serilog.Log.ForContext<YaesuNewCatTransport>();
    private readonly SerialPort _port;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly StringBuilder _rxBuffer = new();

    public YaesuNewCatTransport(string portName, int baudRate)
    {
        _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.Two)
        {
            Handshake = Handshake.RequestToSend,
            ReadTimeout = 200,
            WriteTimeout = 1000,
            DtrEnable = false,
            RtsEnable = true,
            NewLine = ";"
        };
    }

    public bool IsOpen => _port.IsOpen;

    public void Open()
    {
        if (!_port.IsOpen)
            _port.Open();
    }

    public bool SendCommand(string command, int postDelayMs = 50) =>
        Transact(command, postDelayMs) is not null;

    public string? Transact(string command, int postDelayMs = 50)
    {
        if (string.IsNullOrWhiteSpace(command))
            return null;

        var cmd = command.TrimEnd();
        if (!cmd.EndsWith(';'))
            cmd += ';';

        _gate.Wait();
        try
        {
            if (!_port.IsOpen)
                return null;

            for (var attempt = 0; attempt < 2; attempt++)
            {
                _port.DiscardInBuffer();
                _rxBuffer.Clear();
                _port.Write(cmd);

                var reply = ReadUntilSemicolon(postDelayMs);
                if (reply is null)
                    return null;

                if (IsSyntaxError(reply))
                {
                    if (attempt == 0)
                    {
                        Log.Debug("Yaesu newcat {Cmd} rejected ({Reply}), retrying once", cmd, reply);
                        continue;
                    }

                    Log.Warning("Yaesu newcat command failed: {Cmd} → {Reply}", cmd, reply);
                    return null;
                }

                return reply;
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Yaesu newcat transaction failed on {Port} for {Cmd}", _port.PortName, command);
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private string? ReadUntilSemicolon(int postDelayMs)
    {
        var deadline = Environment.TickCount64 + Math.Max(postDelayMs, 50) + 800;
        while (Environment.TickCount64 < deadline)
        {
            try
            {
                while (_port.BytesToRead > 0)
                {
                    var chunk = _port.ReadExisting();
                    if (string.IsNullOrEmpty(chunk))
                        break;
                    _rxBuffer.Append(chunk);
                }
            }
            catch (TimeoutException)
            {
                // keep polling until deadline
            }

            var text = _rxBuffer.ToString();
            var semi = text.IndexOf(';');
            if (semi >= 0)
                return text[..(semi + 1)].Trim();

            Thread.Sleep(15);
        }

        var partial = _rxBuffer.ToString().Trim();
        return partial.Length > 0 ? partial : null;
    }

    private static bool IsSyntaxError(string reply) =>
        reply.Contains("?;", StringComparison.Ordinal);

    public void Dispose()
    {
        try
        {
            if (_port.IsOpen)
                _port.Close();
        }
        catch
        {
            // ignore
        }

        _port.Dispose();
        _gate.Dispose();
    }
}
