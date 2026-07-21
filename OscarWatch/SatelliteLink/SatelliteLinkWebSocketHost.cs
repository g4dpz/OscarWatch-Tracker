using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using OscarWatch.Core.Models;
using OscarWatch.Core.SatelliteLink;
using Serilog;

namespace OscarWatch.SatelliteLink;

public sealed class SatelliteLinkWebSocketHost : IAsyncDisposable
{
    private static readonly ILogger Log = Serilog.Log.ForContext<SatelliteLinkWebSocketHost>();
    private const int HandshakeTimeoutMs = 5000;
    private static readonly TimeSpan SocketCloseTimeout = TimeSpan.FromSeconds(2);

    private readonly object _gate = new();
    private readonly ConcurrentDictionary<Guid, WebSocket> _clients = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;
    private string? _lastError;
    private string? _latestPayload;

    public event Action? StateChanged;

    public bool IsListening
    {
        get { lock (_gate) return _listener is not null; }
    }

    public int ClientCount => _clients.Count;

    public string? LastError
    {
        get { lock (_gate) return _lastError; }
    }

    public async Task StartAsync(SatelliteLinkSettings settings, CancellationToken cancellationToken = default)
    {
        await StopAsync().ConfigureAwait(false);

        var port = SatelliteLinkSettings.NormalizePort(settings.Port);
        var listener = settings.AllowLanClients
            ? new TcpListener(IPAddress.Any, port)
            : new TcpListener(IPAddress.Loopback, port);

        try
        {
            listener.Start();
        }
        catch (Exception ex)
        {
            var detail = SatelliteLinkListenPrefixBuilder.DescribeBindFailure(ex);
            SetError(detail);
            throw new InvalidOperationException(detail, ex);
        }

        lock (_gate)
        {
            _listener = listener;
            _lastError = null;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token), CancellationToken.None);
        NotifyStateChanged();
        Log.Information(
            "Satellite link WebSocket listening on TCP {BindAddress}:{Port}",
            settings.AllowLanClients ? "0.0.0.0" : "127.0.0.1",
            port);
    }

    public async Task StopAsync()
    {
        Task? loop;
        CancellationTokenSource? cts;
        TcpListener? listener;

        lock (_gate)
        {
            loop = _acceptLoop;
            cts = _cts;
            listener = _listener;
            _acceptLoop = null;
            _cts = null;
            _listener = null;
        }

        cts?.Cancel();

        if (listener is not null)
        {
            try
            {
                listener.Stop();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Satellite link listener stop");
            }
        }

        if (loop is not null)
        {
            try
            {
                await loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Satellite link accept loop ended");
            }
        }

        cts?.Dispose();

        foreach (var (id, socket) in _clients.ToArray())
        {
            _clients.TryRemove(id, out _);
            await CloseSocketAsync(socket).ConfigureAwait(false);
        }

        NotifyStateChanged();
    }

    public async Task BroadcastAsync(string jsonPayload, CancellationToken cancellationToken = default)
    {
        _latestPayload = jsonPayload;

        if (_clients.IsEmpty)
            return;

        var bytes = Encoding.UTF8.GetBytes(jsonPayload);
        var segment = new ArraySegment<byte>(bytes);

        foreach (var (id, socket) in _clients.ToArray())
        {
            if (socket.State != WebSocketState.Open)
            {
                _clients.TryRemove(id, out _);
                continue;
            }

            try
            {
                await socket.SendAsync(segment, WebSocketMessageType.Text, endOfMessage: true, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Satellite link send failed; removing client");
                _clients.TryRemove(id, out _);
                await CloseSocketAsync(socket).ConfigureAwait(false);
            }
        }

        NotifyStateChanged();
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpListener? listener;
            lock (_gate)
                listener = _listener;

            if (listener is null)
                break;

            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException ex) when (cancellationToken.IsCancellationRequested)
            {
                Log.Debug(ex, "Satellite link accept cancelled");
                break;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                SetError(ex.Message);
                Log.Warning(ex, "Satellite link accept failed");
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
                continue;
            }

            _ = Task.Run(() => HandleClientAsync(client, cancellationToken), CancellationToken.None);
        }
    }

    private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        WebSocket? socket = null;
        try
        {
            await using var networkStream = tcpClient.GetStream();
            networkStream.ReadTimeout = HandshakeTimeoutMs;
            networkStream.WriteTimeout = HandshakeTimeoutMs;

            socket = await AcceptWebSocketAsync(networkStream, cancellationToken).ConfigureAwait(false);
            if (socket is null)
                return;

            var id = Guid.NewGuid();
            _clients[id] = socket;
            NotifyStateChanged();

            if (!string.IsNullOrEmpty(_latestPayload))
            {
                try
                {
                    var bytes = Encoding.UTF8.GetBytes(_latestPayload);
                    await socket.SendAsync(
                            new ArraySegment<byte>(bytes),
                            WebSocketMessageType.Text,
                            endOfMessage: true,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Satellite link initial snapshot failed");
                }
            }

            var buffer = new byte[256];
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken)
                    .ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;
            }
        }
        catch (WebSocketException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Satellite link client session ended");
        }
        finally
        {
            if (socket is not null)
            {
                foreach (var entry in _clients.Where(pair => ReferenceEquals(pair.Value, socket)).ToArray())
                    _clients.TryRemove(entry.Key, out _);

                await CloseSocketAsync(socket).ConfigureAwait(false);
                NotifyStateChanged();
            }

            tcpClient.Dispose();
        }
    }

    private static async Task<WebSocket?> AcceptWebSocketAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (requestLine is null
            || !requestLine.StartsWith("GET ", StringComparison.Ordinal)
            || !requestLine.Contains('/', StringComparison.Ordinal))
        {
            return null;
        }

        string? webSocketKey = null;
        while (true)
        {
            var headerLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(headerLine))
                break;

            if (headerLine.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
                webSocketKey = headerLine["Sec-WebSocket-Key:".Length..].Trim();
        }

        if (string.IsNullOrWhiteSpace(webSocketKey))
            return null;

        var acceptKey = ComputeWebSocketAcceptKey(webSocketKey);
        var response =
            "HTTP/1.1 101 Switching Protocols\r\n" +
            "Connection: Upgrade\r\n" +
            "Upgrade: websocket\r\n" +
            $"Sec-WebSocket-Accept: {acceptKey}\r\n\r\n";
        var responseBytes = Encoding.ASCII.GetBytes(response);
        await stream.WriteAsync(responseBytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        return WebSocket.CreateFromStream(
            stream,
            new WebSocketCreationOptions
            {
                IsServer = true,
                KeepAliveInterval = TimeSpan.FromSeconds(30)
            });
    }

    private static string ComputeWebSocketAcceptKey(string webSocketKey)
    {
        const string magic = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        var combined = Encoding.UTF8.GetBytes(webSocketKey.Trim() + magic);
        var hash = SHA1.HashData(combined);
        return Convert.ToBase64String(hash);
    }

    private static async Task CloseSocketAsync(WebSocket socket)
    {
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                using var cts = new CancellationTokenSource(SocketCloseTimeout);
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", cts.Token)
                    .ConfigureAwait(false);
            }
        }
        catch
        {
        }
        finally
        {
            socket.Dispose();
        }
    }

    private void SetError(string message)
    {
        lock (_gate)
            _lastError = message;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();
}
