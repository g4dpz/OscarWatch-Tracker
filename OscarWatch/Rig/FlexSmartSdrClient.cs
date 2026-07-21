using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using Serilog;

namespace OscarWatch.Rig;

/// <summary>SmartSDR TCP/IP client (command/response + status cache).</summary>
internal sealed class FlexSmartSdrClient : IDisposable
{
    private const int DefaultCommandTimeoutMs = 2000;
    private const int DefaultConnectTimeoutMs = 5000;

    private static readonly ILogger Log = Serilog.Log.ForContext<FlexSmartSdrClient>();

    private readonly string _host;
    private readonly int _port;
    private readonly int _commandTimeoutMs;
    private readonly int _connectTimeoutMs;
    private readonly object _gate = new();
    private readonly Dictionary<int, FlexSliceState> _slices = new();
    private readonly StringBuilder _lineBuffer = new();
    private readonly byte[] _readBuffer = new byte[4096];

    private TcpClient? _client;
    private NetworkStream? _stream;
    private uint _nextSequence = 1;
    private string _handle = "";
    private string _version = "";
    private bool _fullDuplexEnabled;
    private bool _connected;

    public FlexSmartSdrClient(string host, int port, int commandTimeoutMs = DefaultCommandTimeoutMs)
    {
        _host = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host.Trim();
        _port = port > 0 ? port : FlexSmartSdrCodec.DefaultApiPort;
        _commandTimeoutMs = commandTimeoutMs > 0 ? commandTimeoutMs : DefaultCommandTimeoutMs;
        _connectTimeoutMs = Math.Max(_commandTimeoutMs, DefaultConnectTimeoutMs);
    }

    public bool IsConnected
    {
        get
        {
            lock (_gate)
                return _connected && _client?.Connected == true && _stream is not null;
        }
    }

    public string Handle
    {
        get
        {
            lock (_gate)
                return _handle;
        }
    }

    public string Version
    {
        get
        {
            lock (_gate)
                return _version;
        }
    }

    public bool FullDuplexEnabled
    {
        get
        {
            lock (_gate)
                return _fullDuplexEnabled;
        }
    }

    public IReadOnlyDictionary<int, FlexSliceState> SlicesSnapshot
    {
        get
        {
            lock (_gate)
                return new Dictionary<int, FlexSliceState>(_slices);
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
            _connected = true;
            _lineBuffer.Clear();
            _slices.Clear();
            _nextSequence = 1;

            ReadPrologueUnlocked();
            SendAndWaitUnlocked(seq => FlexSmartSdrCodec.BuildClientProgramCommand(seq));
            SendAndWaitUnlocked(seq => FlexSmartSdrCodec.BuildSubSliceAllCommand(seq));
            SendAndWaitUnlocked(seq => FlexSmartSdrCodec.BuildSubRadioAllCommand(seq));
        }
    }

    public bool SetFullDuplex(bool enabled)
    {
        lock (_gate)
        {
            EnsureConnectedUnlocked();
            var ok = SendAndWaitUnlocked(seq => FlexSmartSdrCodec.BuildFullDuplexCommand(seq, enabled));
            if (ok)
                _fullDuplexEnabled = enabled;
            return ok;
        }
    }

    public bool TuneSlice(int sliceIndex, long hz)
    {
        lock (_gate)
        {
            EnsureConnectedUnlocked();
            var mhz = FlexSmartSdrCodec.HzToMhz(hz);
            var ok = SendAndWaitUnlocked(seq => FlexSmartSdrCodec.BuildSliceTuneCommand(seq, sliceIndex, mhz));
            if (ok)
                UpdateSliceFrequencyUnlocked(sliceIndex, hz);
            return ok;
        }
    }

    public bool SetSliceMode(int sliceIndex, string smartSdrMode)
    {
        lock (_gate)
        {
            EnsureConnectedUnlocked();
            var ok = SendAndWaitUnlocked(seq =>
                FlexSmartSdrCodec.BuildSliceSetModeCommand(seq, sliceIndex, smartSdrMode));
            if (ok && _slices.TryGetValue(sliceIndex, out var existing))
            {
                _slices[sliceIndex] = existing with { Mode = smartSdrMode };
            }

            return ok;
        }
    }

    public bool SetSliceTx(int sliceIndex, bool tx)
    {
        lock (_gate)
        {
            EnsureConnectedUnlocked();
            var ok = SendAndWaitUnlocked(seq => FlexSmartSdrCodec.BuildSliceSetTxCommand(seq, sliceIndex, tx));
            if (!ok)
                return false;

            var keys = new List<int>(_slices.Keys);
            foreach (var key in keys)
            {
                var s = _slices[key];
                if (key == sliceIndex)
                    _slices[key] = s with { IsTransmit = tx };
                else if (tx && s.IsTransmit)
                    _slices[key] = s with { IsTransmit = false };
            }

            return true;
        }
    }

    public bool SetSliceTone(int sliceIndex, bool toneOn, double toneHz)
    {
        lock (_gate)
        {
            EnsureConnectedUnlocked();
            return SendAndWaitUnlocked(seq =>
                FlexSmartSdrCodec.BuildSliceSetToneCommand(seq, sliceIndex, toneOn, toneHz));
        }
    }

    public int? CreateSlice(long hz, string mode, string? ant = null)
    {
        lock (_gate)
        {
            EnsureConnectedUnlocked();
            var mhz = FlexSmartSdrCodec.HzToMhz(hz);
            var response = SendAndWaitResponseUnlocked(seq =>
                FlexSmartSdrCodec.BuildSliceCreateCommand(seq, mhz, mode, ant));
            if (response is null || !FlexSmartSdrCodec.IsSuccessResponse(response))
                return null;

            if (FlexSmartSdrCodec.TryParseSliceCreateIndex(response.Body, out var index))
            {
                _slices[index] = new FlexSliceState(
                    index, true, hz, mode, IsTransmit: false, IsActive: false, "", 0);
                return index;
            }

            // Fallback: highest known index + 1 if radio did not echo the index
            var next = _slices.Count == 0 ? 0 : _slices.Keys.Max() + 1;
            _slices[next] = new FlexSliceState(
                next, true, hz, mode, IsTransmit: false, IsActive: false, "", 0);
            return next;
        }
    }

    public long? GetSliceFrequencyHz(int sliceIndex)
    {
        lock (_gate)
        {
            return _slices.TryGetValue(sliceIndex, out var s) && s.FrequencyHz > 0
                ? s.FrequencyHz
                : null;
        }
    }

    public bool TryGetSlice(int sliceIndex, out FlexSliceState slice)
    {
        lock (_gate)
            return _slices.TryGetValue(sliceIndex, out slice!);
    }

    public IReadOnlyList<FlexSliceState> GetInUseSlices()
    {
        lock (_gate)
        {
            return _slices.Values
                .Where(s => s.InUse)
                .OrderBy(s => s.Index)
                .ToList();
        }
    }

    public void Dispose()
    {
        lock (_gate)
            DisconnectUnlocked();
    }

    private void ReadPrologueUnlocked()
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(_commandTimeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (!string.IsNullOrEmpty(_version) && !string.IsNullOrEmpty(_handle))
                return;

            if (!TryReadLineUnlocked(deadline, out var line) || string.IsNullOrEmpty(line))
                continue;

            ProcessIncomingLineUnlocked(line);
        }

        if (string.IsNullOrEmpty(_version) || string.IsNullOrEmpty(_handle))
            throw new InvalidOperationException(
                $"Flex SmartSDR prologue incomplete from {_host}:{_port} (version='{_version}', handle='{_handle}').");
    }

    private bool SendAndWaitUnlocked(Func<uint, string> commandFactory)
    {
        var response = SendAndWaitResponseUnlocked(commandFactory);
        return response is not null && FlexSmartSdrCodec.IsSuccessResponse(response);
    }

    private FlexSmartSdrMessage? SendAndWaitResponseUnlocked(Func<uint, string> commandFactory)
    {
        if (_stream is null)
            return null;

        var seq = _nextSequence++;
        var command = commandFactory(seq);
        var bytes = Encoding.ASCII.GetBytes(command);
        _stream.Write(bytes, 0, bytes.Length);
        _stream.Flush();

        var deadline = DateTime.UtcNow.AddMilliseconds(_commandTimeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (!TryReadLineUnlocked(deadline, out var line) || string.IsNullOrEmpty(line))
                continue;

            var msg = ProcessIncomingLineUnlocked(line);
            if (msg is { Kind: FlexSmartSdrMessageKind.Response } && msg.Sequence == seq)
                return msg;
        }

        Log.Warning("Flex SmartSDR command timed out waiting for R{Seq}", seq);
        return null;
    }

    private FlexSmartSdrMessage? ProcessIncomingLineUnlocked(string line)
    {
        if (!FlexSmartSdrCodec.TryParseLine(line, out var message))
            return null;

        switch (message.Kind)
        {
            case FlexSmartSdrMessageKind.Version:
                _version = message.Body;
                break;
            case FlexSmartSdrMessageKind.Handle:
                _handle = message.Handle;
                break;
            case FlexSmartSdrMessageKind.Status:
                ApplyStatusUnlocked(message.Body);
                break;
        }

        return message;
    }

    private void ApplyStatusUnlocked(string body)
    {
        if (FlexSmartSdrCodec.TryParseSliceStatus(body, out var slice))
        {
            _slices[slice.Index] = slice;
            return;
        }

        if (FlexSmartSdrCodec.TryParseRadioFullDuplex(body, out var fdx))
            _fullDuplexEnabled = fdx;
    }

    private void UpdateSliceFrequencyUnlocked(int sliceIndex, long hz)
    {
        if (_slices.TryGetValue(sliceIndex, out var existing))
            _slices[sliceIndex] = existing with { FrequencyHz = hz, InUse = true };
        else
            _slices[sliceIndex] = new FlexSliceState(
                sliceIndex, true, hz, "", false, false, "", 0);
    }

    private bool TryReadLineUnlocked(DateTime deadline, out string line)
    {
        line = "";
        if (_stream is null)
            return false;

        var timeout = deadline - DateTime.UtcNow;
        if (timeout <= TimeSpan.Zero)
            return false;

        var sw = Stopwatch.StartNew();
        var timeoutMs = (long)timeout.TotalMilliseconds;

        while (true)
        {
            var buffered = ExtractLineFromBuffer();
            if (buffered is not null)
            {
                line = buffered;
                return true;
            }

            if (sw.ElapsedMilliseconds >= timeoutMs)
                return false;

            var remainingMs = (int)Math.Max(1, timeoutMs - sw.ElapsedMilliseconds);
            var saved = _stream.ReadTimeout;
            _stream.ReadTimeout = remainingMs;
            try
            {
                var read = _stream.Read(_readBuffer, 0, _readBuffer.Length);
                if (read <= 0)
                    return false;

                _lineBuffer.Append(Encoding.ASCII.GetString(_readBuffer, 0, read));
            }
            catch (IOException)
            {
                return _lineBuffer.Length > 0 && ExtractLineFromBuffer() is { } partial
                    ? Assign(partial, out line)
                    : false;
            }
            finally
            {
                _stream.ReadTimeout = saved;
            }
        }

        static bool Assign(string value, out string line)
        {
            line = value;
            return true;
        }
    }

    private string? ExtractLineFromBuffer()
    {
        // Scan StringBuilder by index to avoid allocating a full string copy on every call.
        var length = _lineBuffer.Length;
        if (length == 0)
            return null;

        var idx = -1;
        for (var i = 0; i < length; i++)
        {
            var c = _lineBuffer[i];
            if (c is '\r' or '\n')
            {
                idx = i;
                break;
            }
        }

        if (idx < 0)
            return null;

        // Extract the line up to the newline character.
        var line = _lineBuffer.ToString(0, idx);

        // Determine how many characters to skip (handle \r\n as one delimiter).
        var skip = 1;
        if (idx + 1 < length && _lineBuffer[idx] == '\r' && _lineBuffer[idx + 1] == '\n')
            skip = 2;

        // Remove the consumed line + delimiter from the buffer.
        _lineBuffer.Remove(0, idx + skip);

        return line;
    }

    private void EnsureConnectedUnlocked()
    {
        if (_connected && _client?.Connected == true && _stream is not null)
            return;

        throw new InvalidOperationException("Flex SmartSDR client is not connected.");
    }

    private void DisconnectUnlocked()
    {
        _connected = false;
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
        _lineBuffer.Clear();
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
            throw new TimeoutException($"Flex SmartSDR connect to {host}:{port} timed out.");
        }
    }
}
