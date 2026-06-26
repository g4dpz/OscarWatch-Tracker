using System.Globalization;
using System.Net.Sockets;
using System.Text;
using OscarWatch.Core.Models;

namespace OscarWatch.Rig;

/// <summary>Hamlib rigctl TCP client (text commands, newline-terminated).</summary>
internal sealed class RigCtlTcpClient : IDisposable
{
    private const int DefaultCommandTimeoutMs = 500;

    private readonly string _host;
    private readonly int _port;
    private readonly int _commandTimeoutMs;
    private readonly object _gate = new();
    private TcpClient? _client;
    private NetworkStream? _stream;

    public RigCtlTcpClient(string host, int port, int commandTimeoutMs = DefaultCommandTimeoutMs)
    {
        _host = string.IsNullOrWhiteSpace(host) ? RigEndpointSettings.SdrRigCtlDefaultHost : host.Trim();
        _port = port;
        _commandTimeoutMs = commandTimeoutMs > 0 ? commandTimeoutMs : DefaultCommandTimeoutMs;
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
            _client = new TcpClient();
            _client.ReceiveTimeout = _commandTimeoutMs;
            _client.SendTimeout = _commandTimeoutMs;
            if (!_client.ConnectAsync(_host, _port).Wait(_commandTimeoutMs))
                throw new TimeoutException($"rigctl connect to {_host}:{_port} timed out.");

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
        _client = new TcpClient();
        _client.ReceiveTimeout = _commandTimeoutMs;
        _client.SendTimeout = _commandTimeoutMs;
        if (!_client.ConnectAsync(_host, _port).Wait(_commandTimeoutMs))
            throw new TimeoutException($"rigctl reconnect to {_host}:{_port} timed out.");

        _stream = _client.GetStream();
        _stream.ReadTimeout = _commandTimeoutMs;
        _stream.WriteTimeout = _commandTimeoutMs;
    }

    private string ReadResponseUnlocked()
    {
        if (_stream is null)
            return "";

        var builder = new StringBuilder();
        var buffer = new byte[256];
        var deadline = DateTime.UtcNow.AddMilliseconds(_commandTimeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            if (!_stream.DataAvailable)
            {
                Thread.Sleep(5);
                if (builder.Length > 0 && RigCtlResponseParser.LooksComplete(builder.ToString()))
                    break;
                continue;
            }

            var read = _stream.Read(buffer, 0, buffer.Length);
            if (read <= 0)
                break;

            builder.Append(Encoding.ASCII.GetString(buffer, 0, read));
            if (RigCtlResponseParser.LooksComplete(builder.ToString()))
                break;
        }

        return builder.ToString();
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
}

internal static class RigCtlResponseParser
{
    public static bool IsSuccess(string response)
    {
        foreach (var line in SplitLines(response))
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
        foreach (var line in SplitLines(response))
        {
            if (line.StartsWith("RPRT ", StringComparison.Ordinal))
                continue;

            if (long.TryParse(line.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var hz))
                return hz;

            if (double.TryParse(line.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var hzFloat))
                return (long)Math.Round(hzFloat);
        }

        return null;
    }

    public static bool LooksComplete(string response)
    {
        if (string.IsNullOrEmpty(response))
            return false;

        var trimmed = response.TrimEnd('\r', '\n');
        if (!trimmed.Contains('\n', StringComparison.Ordinal))
            return trimmed.StartsWith("RPRT ", StringComparison.Ordinal)
                || long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                || double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

        foreach (var line in SplitLines(response))
        {
            if (line.StartsWith("RPRT ", StringComparison.Ordinal)
                || long.TryParse(line.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                || double.TryParse(line.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                return true;
        }

        return false;
    }

    private static IEnumerable<string> SplitLines(string response) =>
        response.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
