using System.Globalization;
using System.Net.Sockets;
using System.Text;
using OscarWatch.Core.Models;

namespace OscarWatch.Rig;

/// <summary>Hamlib rigctl TCP client (text commands, newline-terminated).</summary>
internal sealed class RigCtlTcpClient : IDisposable
{
    private const int DefaultCommandTimeoutMs = 500;
    private const int DefaultConnectTimeoutMs = 3000;

    private readonly string _host;
    private readonly int _port;
    private readonly int _commandTimeoutMs;
    private readonly int _connectTimeoutMs;
    private readonly object _gate = new();
    private readonly byte[] _readBuffer = new byte[256];
    private readonly StringBuilder _responseBuilder = new();
    private TcpClient? _client;
    private NetworkStream? _stream;

    public RigCtlTcpClient(string host, int port, int commandTimeoutMs = DefaultCommandTimeoutMs)
    {
        _host = string.IsNullOrWhiteSpace(host) ? RigEndpointSettings.SdrRigCtlDefaultHost : host.Trim();
        _port = port;
        _commandTimeoutMs = commandTimeoutMs > 0 ? commandTimeoutMs : DefaultCommandTimeoutMs;
        _connectTimeoutMs = Math.Max(_commandTimeoutMs, DefaultConnectTimeoutMs);
    }

    public bool IsConnected
    {
        get
        {
            lock (_gate)
                return _client?.Connected == true && _stream is not null;
        }
    }

    public void Open()
    {
        lock (_gate)
        {
            DisconnectUnlocked();
            _client = new TcpClient { NoDelay = true };
            _client.ReceiveTimeout = _commandTimeoutMs;
            _client.SendTimeout = _commandTimeoutMs;
            ConnectWithTimeout(_client, _host, _port, _connectTimeoutMs);

            _stream = _client.GetStream();
            _stream.ReadTimeout = _commandTimeoutMs;
            _stream.WriteTimeout = _commandTimeoutMs;
        }
    }

    public bool SetFrequencyHz(long hz)
    {
        var response = SendCommand($"F {hz.ToString(CultureInfo.InvariantCulture)}\n");
        return RigCtlResponseParser.IsSuccess(response);
    }

    public long? ReadFrequencyHz()
    {
        var response = SendCommand("f\n");
        return RigCtlResponseParser.TryParseFrequencyHz(response);
    }

    public bool SetMode(string hamlibMode)
    {
        if (string.IsNullOrWhiteSpace(hamlibMode))
            return true;

        var response = SendCommand($"M {hamlibMode} 0\n");
        return RigCtlResponseParser.IsSuccess(response);
    }

    public void Dispose()
    {
        lock (_gate)
            DisconnectUnlocked();
    }

    private string SendCommand(string command)
    {
        lock (_gate)
        {
            EnsureConnectedUnlocked();
            var bytes = Encoding.ASCII.GetBytes(command);
            _stream!.Write(bytes, 0, bytes.Length);
            _stream.Flush();
            return ReadResponseUnlocked();
        }
    }

    private void EnsureConnectedUnlocked()
    {
        if (_client?.Connected == true && _stream is not null)
            return;

        DisconnectUnlocked();
        _client = new TcpClient { NoDelay = true };
        _client.ReceiveTimeout = _commandTimeoutMs;
        _client.SendTimeout = _commandTimeoutMs;
        ConnectWithTimeout(_client, _host, _port, _connectTimeoutMs);

        _stream = _client.GetStream();
        _stream.ReadTimeout = _commandTimeoutMs;
        _stream.WriteTimeout = _commandTimeoutMs;
    }

    private string ReadResponseUnlocked()
    {
        if (_stream is null)
            return "";

        _responseBuilder.Clear();
        var savedTimeout = _stream.ReadTimeout;
        var deadline = DateTime.UtcNow.AddMilliseconds(_commandTimeoutMs);

        try
        {
            while (DateTime.UtcNow < deadline)
            {
                if (_responseBuilder.Length > 0 && LooksCompleteSpan(_responseBuilder))
                {
                    var text = _responseBuilder.ToString();
                    if (RigCtlResponseParser.LooksComplete(text))
                        break;
                }

                var remainingMs = (int)Math.Max(1, (deadline - DateTime.UtcNow).TotalMilliseconds);
                _stream.ReadTimeout = remainingMs;

                try
                {
                    var read = _stream.Read(_readBuffer, 0, _readBuffer.Length);
                    if (read <= 0)
                        break;

                    _responseBuilder.Append(Encoding.ASCII.GetString(_readBuffer, 0, read));

                    // Only check completeness if we received a newline (saves ToString allocations)
                    if (ContainsNewline(_readBuffer, read))
                    {
                        var text = _responseBuilder.ToString();
                        if (RigCtlResponseParser.LooksComplete(text))
                            break;
                    }
                }
                catch (IOException)
                {
                    if (_responseBuilder.Length > 0 && RigCtlResponseParser.LooksComplete(_responseBuilder.ToString()))
                        break;

                    if (_responseBuilder.Length == 0)
                        return string.Empty;

                    break;
                }
            }
        }
        finally
        {
            _stream.ReadTimeout = savedTimeout;
        }

        return _responseBuilder.ToString();
    }

    private static bool LooksCompleteSpan(StringBuilder sb)
    {
        if (sb.Length == 0) return false;
        return sb[sb.Length - 1] == '\n';
    }

    private static bool ContainsNewline(byte[] buffer, int count)
    {
        for (var i = 0; i < count; i++)
            if (buffer[i] == (byte)'\n')
                return true;
        return false;
    }

    private void DisconnectUnlocked()
    {
        try
        {
            _stream?.Dispose();
        }
        catch
        {
        }

        try
        {
            _client?.Dispose();
        }
        catch
        {
        }

        _stream = null;
        _client = null;
    }

    private static void ConnectWithTimeout(TcpClient client, string host, int port, int timeoutMs)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            client.ConnectAsync(host, port, cts.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"rigctl connect to {host}:{port} timed out.");
        }
    }
}

internal static class RigCtlResponseParser
{
    public static bool IsSuccess(string response)
    {
        foreach (var line in EnumerateLines(response))
        {
            if (!line.StartsWith("RPRT ", StringComparison.Ordinal))
                continue;

            return int.TryParse(line.AsSpan(5), NumberStyles.Integer, CultureInfo.InvariantCulture, out var code)
                && code == 0;
        }

        return false;
    }

    public static long? TryParseFrequencyHz(string response)
    {
        foreach (var line in EnumerateLines(response))
        {
            if (line.StartsWith("RPRT ", StringComparison.Ordinal))
                continue;

            var span = line.AsSpan();
            if (long.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hz))
                return hz;

            if (double.TryParse(span, NumberStyles.Float, CultureInfo.InvariantCulture, out var hzFloat))
                return (long)Math.Round(hzFloat);
        }

        return null;
    }

    public static bool LooksComplete(string response)
    {
        if (string.IsNullOrEmpty(response))
            return false;

        var trimmed = response.AsSpan().TrimEnd("\r\n".AsSpan());
        if (!trimmed.Contains('\n'))
        {
            return trimmed.StartsWith("RPRT ", StringComparison.Ordinal)
                || long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                || double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        }

        foreach (var line in EnumerateLines(response))
        {
            if (line.StartsWith("RPRT ", StringComparison.Ordinal)
                || long.TryParse(line.AsSpan(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                || double.TryParse(line.AsSpan(), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Splits response into lines without allocating intermediate trimmed strings.
    /// Uses span-based trimming where possible; returns the non-empty trimmed lines.
    /// </summary>
    private static IEnumerable<string> EnumerateLines(string response)
    {
        var start = 0;
        var length = response.Length;

        while (start < length)
        {
            var end = response.IndexOfAny(['\r', '\n'], start);
            if (end < 0)
                end = length;

            var segment = response.AsSpan(start, end - start).Trim();
            if (segment.Length > 0)
                yield return segment.ToString();

            // Skip \r\n or single \r or \n
            if (end < length && response[end] == '\r' && end + 1 < length && response[end + 1] == '\n')
                start = end + 2;
            else
                start = end + 1;
        }
    }
}

internal static class RigCtlModeMapper
{
    public static string? ToHamlibMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
            return null;

        return mode.Trim().ToUpperInvariant() switch
        {
            "USB" => "USB",
            "LSB" => "LSB",
            "FM" or "FMN" or "NFM" => "FM",
            "CW" => "CW",
            "AM" => "AM",
            "PKTUSB" or "PKTLSB" => "PKTUSB",
            _ => mode.Trim().ToUpperInvariant()
        };
    }
}
