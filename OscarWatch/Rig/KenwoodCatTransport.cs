using System.IO.Ports;
using System.Text;
using OscarWatch.Core.Radio;
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
        SendFireAndForget(command, postDelayMs);

    public bool SendFireAndForget(string command, int postDelayMs = 50)
    {
        if (string.IsNullOrWhiteSpace(command))
            return false;

        var cmd = NormalizeCommand(command);
        _gate.Wait();
        try
        {
            if (!_port.IsOpen)
                return false;

            _port.Write(cmd);
            Thread.Sleep(Math.Max(postDelayMs, 0));
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Kenwood CAT send failed on {Port} for {Cmd}", _port.PortName, command);
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

        var cmd = NormalizeCommand(command);
        var readTimeoutMs = KenwoodCatCodec.GetReplyTimeoutMs(cmd, postDelayMs);

        _gate.Wait();
        try
        {
            if (!_port.IsOpen)
                return null;

            for (var attempt = 0; attempt < 2; attempt++)
            {
                DrainInputBuffer();
                _rxBuffer.Clear();
                _port.Write(cmd);

                var reply = ReadUntilSemicolon(readTimeoutMs);
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

    private void DrainInputBuffer()
    {
        try
        {
            _port.DiscardInBuffer();
            _rxBuffer.Clear();
        }
        catch
        {
            // ignore
        }
    }

    private string? ReadUntilSemicolon(int readTimeoutMs)
    {
        var deadline = Environment.TickCount64 + readTimeoutMs;
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

    private static string NormalizeCommand(string command)
    {
        var cmd = command.TrimEnd();
        return cmd.EndsWith(';') ? cmd : cmd + ";";
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
