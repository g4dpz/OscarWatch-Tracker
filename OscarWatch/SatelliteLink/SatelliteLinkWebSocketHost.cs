using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using OscarWatch.Core.Models;
using Serilog;

namespace OscarWatch.SatelliteLink;

public sealed class SatelliteLinkWebSocketHost : IAsyncDisposable
{
    private static readonly ILogger Log = Serilog.Log.ForContext<SatelliteLinkWebSocketHost>();
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<Guid, WebSocket> _clients = new();
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;
    private string? _lastError;
    private string? _latestPayload;

    public event Action? StateChanged;

    public bool IsListening
    {
        get { lock (_gate) return _listener?.IsListening == true; }
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
        var prefix = settings.AllowLanClients
            ? $"http://+:{port}/"
            : $"http://127.0.0.1:{port}/";

        var listener = new HttpListener();
        listener.Prefixes.Add(prefix);

        try
        {
            listener.Start();
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
            listener.Close();
            throw;
        }

        lock (_gate)
        {
            _listener = listener;
            _lastError = null;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token), CancellationToken.None);
        NotifyStateChanged();
        Log.Information("Satellite link WebSocket listening on {Prefix}", prefix);
    }

    public async Task StopAsync()
    {
        Task? loop;
        CancellationTokenSource? cts;
        HttpListener? listener;

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
                listener.Close();
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
            HttpListener? listener;
            lock (_gate)
                listener = _listener;

            if (listener is null || !listener.IsListening)
                break;

            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
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

            _ = Task.Run(() => HandleRequestAsync(context, cancellationToken), CancellationToken.None);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;
        if (!request.IsWebSocketRequest)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.Close();
            return;
        }

        HttpListenerWebSocketContext? wsContext;
        try
        {
            wsContext = await context.AcceptWebSocketAsync(subProtocol: null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Satellite link WebSocket handshake failed");
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.Close();
            return;
        }

        var socket = wsContext.WebSocket;
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

        try
        {
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
            Log.Debug(ex, "Satellite link client receive loop ended");
        }
        finally
        {
            _clients.TryRemove(id, out _);
            await CloseSocketAsync(socket).ConfigureAwait(false);
            NotifyStateChanged();
        }
    }

    private static async Task CloseSocketAsync(WebSocket socket)
    {
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None)
                    .ConfigureAwait(false);
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
