using System.Net.Sockets;
using System.Text;
using OscarWatch.Core.Models;

namespace OscarWatch.Rotator;

/// <summary>TCP client for OZ9AAR URC JSON socket protocol (request → status reply).</summary>
internal sealed class UrcTcpClient : IDisposable
{
    private const int DefaultCommandTimeoutMs = 1500;
    private const int DefaultConnectTimeoutMs = 3000;

    private readonly string _host;
    private readonly int _port;
    private readonly int _commandTimeoutMs;
    private readonly int _connectTimeoutMs;
    private readonly object _gate = new();
    private readonly byte[] _readBuffer = new byte[1024];
    private readonly StringBuilder _rxBuffer = new();
    private TcpClient? _client;
    private NetworkStream? _stream;

    public UrcTcpClient(string host, int port, int commandTimeoutMs = DefaultCommandTimeoutMs)
    {
        _host = string.IsNullOrWhiteSpace(host) ? RotatorSettings.DefaultNetworkHost : host.Trim();
        _port = port > 0 ? port : RotatorSettings.DefaultNetworkPort;
        _commandTimeoutMs = commandTimeoutMs > 0 ? commandTimeoutMs : DefaultCommandTimeoutMs;
        _connectTimeoutMs = Math.Max(_commandTimeoutMs, DefaultConnectTimeoutMs);
    }

    public string EndpointDisplay => $"{_host}:{_port}";

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
            _rxBuffer.Clear();
        }
    }

    /// <summary>Send a JSON request and read one status object reply.</summary>
    public string Transact(string requestJson)
    {
        lock (_gate)
        {
            EnsureConnectedUnlocked();
            var bytes = Encoding.UTF8.GetBytes(requestJson);
            _stream!.Write(bytes, 0, bytes.Length);
            _stream.Flush();
            return ReadJsonObjectUnlocked();
        }
    }

    public void Dispose()
    {
        lock (_gate)
            DisconnectUnlocked();
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
        _rxBuffer.Clear();
    }

    private string ReadJsonObjectUnlocked()
    {
        if (_stream is null)
            return "";

        var savedTimeout = _stream.ReadTimeout;
        var deadline = DateTime.UtcNow.AddMilliseconds(_commandTimeoutMs);

        try
        {
            while (DateTime.UtcNow < deadline)
            {
                if (UrcJsonCodec.TryExtractCompleteObject(_rxBuffer, out var json))
                    return json;

                var remainingMs = (int)Math.Max(1, (deadline - DateTime.UtcNow).TotalMilliseconds);
                _stream.ReadTimeout = remainingMs;

                try
                {
                    var read = _stream.Read(_readBuffer, 0, _readBuffer.Length);
                    if (read <= 0)
                        break;

                    _rxBuffer.Append(Encoding.UTF8.GetString(_readBuffer, 0, read));
                    if (UrcJsonCodec.TryExtractCompleteObject(_rxBuffer, out json))
                        return json;
                }
                catch (IOException)
                {
                    if (UrcJsonCodec.TryExtractCompleteObject(_rxBuffer, out var partial))
                        return partial;
                    break;
                }
            }

            if (UrcJsonCodec.TryExtractCompleteObject(_rxBuffer, out var leftover))
                return leftover;

            return "";
        }
        finally
        {
            _stream.ReadTimeout = savedTimeout;
        }
    }

    private void DisconnectUnlocked()
    {
        try { _stream?.Dispose(); } catch { /* ignore */ }
        try { _client?.Dispose(); } catch { /* ignore */ }
        _stream = null;
        _client = null;
        _rxBuffer.Clear();
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
            throw new TimeoutException($"URC connect to {host}:{port} timed out.");
        }
    }
}
