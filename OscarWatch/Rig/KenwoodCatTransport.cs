using System.IO.Ports;
using System.Text;
using Serilog;

namespace OscarWatch.Rig;

/// <summary>Kenwood TS-2000 CAT: 8N1 ASCII commands terminated by semicolon.</summary>
internal sealed class KenwoodCatTransport : IKenwoodCatTransport
{
    private static readonly ILogger Log = Serilog.Log.ForContext<KenwoodCatTransport>();
    private readonly SerialPort _port;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly StringBuilder _rxBuffer = new();

    public KenwoodCatTransport(string portName, int baudRate)
    {
        _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            ReadTimeout = 200,
            WriteTimeout = 1000,
            DtrEnable = false,
            RtsEnable = false,
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
                        Log.Debug("Kenwood CAT {Cmd} rejected ({Reply}), retrying once", cmd, reply);
                        continue;
                    }

                    Log.Warning(
                        "Kenwood CAT command failed: {Cmd} → {Reply}. Close any radio menu and confirm SATL if tracking.",
                        cmd,
                        reply);
                    return null;
                }

                if (IsProcessingIncomplete(reply))
                {
                    Thread.Sleep(postDelayMs);
                    continue;
                }

                return reply;
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Kenwood CAT transaction failed on {Port} for {Cmd}", _port.PortName, command);
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
        reply.Contains("?;", StringComparison.Ordinal) || reply.Contains("E;", StringComparison.Ordinal);

    private static bool IsProcessingIncomplete(string reply) =>
        reply.Contains("O;", StringComparison.OrdinalIgnoreCase);

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
