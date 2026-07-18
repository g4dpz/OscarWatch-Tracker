using System.Net.Sockets;
using System.Text;
using OscarWatch.Core.Models;

namespace OscarWatch.Rotator;

/// <summary>Raw TCP byte stream for ser2net-style serial tunnels (not URC JSON).</summary>
internal sealed class TcpRotatorTransport : IRotatorSerialTransport
{
    private const int DefaultConnectTimeoutMs = 3000;

    private readonly string _host;
    private readonly int _port;
    private readonly int _readTimeoutMs;
    private readonly int _writeTimeoutMs;
    private readonly int _connectTimeoutMs;
    private readonly string _newLine;
    private readonly object _gate = new();
    private readonly byte[] _readBuffer = new byte[1024];
    private readonly StringBuilder _rxBuffer = new();

    private TcpClient? _client;
    private NetworkStream? _stream;

    public TcpRotatorTransport(
        string host,
        int port,
        int readTimeoutMs,
        int writeTimeoutMs,
        string newLine)
    {
        _host = string.IsNullOrWhiteSpace(host) ? RotatorSettings.DefaultNetworkHost : host.Trim();
        _port = port > 0 ? port : RotatorSettings.DefaultNetworkPort;
        _readTimeoutMs = readTimeoutMs > 0 ? readTimeoutMs : 1000;
        _writeTimeoutMs = writeTimeoutMs > 0 ? writeTimeoutMs : 1000;
        _connectTimeoutMs = Math.Max(_readTimeoutMs, DefaultConnectTimeoutMs);
        _newLine = string.IsNullOrEmpty(newLine) ? "\n" : newLine;
    }

    public bool IsOpen
    {
        get
        {
            lock (_gate)
                return _client?.Connected == true && _stream is not null;
        }
    }

    public bool DtrEnable { get; set; }

    public bool RtsEnable { get; set; }

    public void Open()
    {
        lock (_gate)
        {
            DisconnectUnlocked();
            ConnectUnlocked();
        }
    }

    public void Write(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        Write(bytes, 0, bytes.Length);
    }

    public void Write(byte[] buffer, int offset, int count)
    {
        lock (_gate)
        {
            EnsureConnectedUnlocked();
            _stream!.Write(buffer, offset, count);
            _stream.Flush();
        }
    }

    public void DiscardInBuffer()
    {
        lock (_gate)
        {
            _rxBuffer.Clear();
            if (_stream is null || !_stream.CanRead)
                return;

            try
            {
                while (_stream.DataAvailable)
                {
                    var read = _stream.Read(_readBuffer, 0, _readBuffer.Length);
                    if (read <= 0)
                        break;
                }
            }
            catch
            {
                // ignore drain errors
            }
        }
    }

    public void DiscardOutBuffer()
    {
        // TCP has no local out buffer to discard.
    }

    public string ReadLine()
    {
        lock (_gate)
        {
            EnsureConnectedUnlocked();
            var deadline = DateTime.UtcNow.AddMilliseconds(_readTimeoutMs);
            var savedTimeout = _stream!.ReadTimeout;

            try
            {
                while (DateTime.UtcNow < deadline)
                {
                    if (TryTakeLine(out var line))
                        return line;

                    var remainingMs = (int)Math.Max(1, (deadline - DateTime.UtcNow).TotalMilliseconds);
                    _stream.ReadTimeout = remainingMs;

                    try
                    {
                        var read = _stream.Read(_readBuffer, 0, _readBuffer.Length);
                        if (read <= 0)
                            break;

                        _rxBuffer.Append(Encoding.UTF8.GetString(_readBuffer, 0, read));
                        if (TryTakeLine(out line))
                            return line;
                    }
                    catch (IOException)
                    {
                        break;
                    }
                }

                throw new TimeoutException("TCP serial ReadLine timed out.");
            }
            finally
            {
                _stream.ReadTimeout = savedTimeout;
            }
        }
    }

    public string ReadExisting()
    {
        lock (_gate)
        {
            if (_stream is null)
                return "";

            try
            {
                while (_stream.DataAvailable)
                {
                    var read = _stream.Read(_readBuffer, 0, _readBuffer.Length);
                    if (read <= 0)
                        break;
                    _rxBuffer.Append(Encoding.UTF8.GetString(_readBuffer, 0, read));
                }
            }
            catch
            {
                // return what we have
            }

            var text = _rxBuffer.ToString();
            _rxBuffer.Clear();
            return text;
        }
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        lock (_gate)
        {
            EnsureConnectedUnlocked();

            // Prefer any leftover bytes from prior line reads.
            if (_rxBuffer.Length > 0)
            {
                var leftover = Encoding.UTF8.GetBytes(_rxBuffer.ToString());
                _rxBuffer.Clear();
                var fromBuffer = Math.Min(count, leftover.Length);
                Array.Copy(leftover, 0, buffer, offset, fromBuffer);
                if (fromBuffer < leftover.Length)
                    _rxBuffer.Append(Encoding.UTF8.GetString(leftover, fromBuffer, leftover.Length - fromBuffer));
                return fromBuffer;
            }

            return _stream!.Read(buffer, offset, count);
        }
    }

    public void Dispose()
    {
        lock (_gate)
            DisconnectUnlocked();
    }

    private bool TryTakeLine(out string line)
    {
        line = "";
        var text = _rxBuffer.ToString();
        var index = text.IndexOf(_newLine, StringComparison.Ordinal);
        if (index < 0)
            return false;

        line = text[..index];
        var consumed = index + _newLine.Length;
        _rxBuffer.Clear();
        if (consumed < text.Length)
            _rxBuffer.Append(text[consumed..]);
        return true;
    }

    private void EnsureConnectedUnlocked()
    {
        if (_client?.Connected == true && _stream is not null)
            return;

        DisconnectUnlocked();
        ConnectUnlocked();
    }

    private void ConnectUnlocked()
    {
        _client = new TcpClient { NoDelay = true };
        _client.ReceiveTimeout = _readTimeoutMs;
        _client.SendTimeout = _writeTimeoutMs;
        ConnectWithTimeout(_client, _host, _port, _connectTimeoutMs);

        _stream = _client.GetStream();
        _stream.ReadTimeout = _readTimeoutMs;
        _stream.WriteTimeout = _writeTimeoutMs;
        _rxBuffer.Clear();
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
            throw new TimeoutException($"TCP serial connect to {host}:{port} timed out.");
        }
    }
}
