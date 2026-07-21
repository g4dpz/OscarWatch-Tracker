using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using OscarWatch.Core.Radio;

namespace OscarWatch.Tests;

/// <summary>Minimal SmartSDR TCP stub for hardware-less Flex driver tests.</summary>
internal sealed class FlexSmartSdrStubServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _serverTask;
    private readonly TaskCompletionSource _acceptLoopReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentDictionary<int, StubSlice> _slices = new();
    private readonly object _gate = new();
    private int _nextSliceIndex;
    private bool _fullDuplex;
    private readonly bool _rejectFullDuplex;
    private readonly bool _rejectTxSlice;
    private readonly bool _rejectSliceCreate;
    private readonly bool _omitSliceCreateIndex;
    private readonly bool _emitPartialSliceStatus;
    private readonly bool _rejectClientProgram;

    public FlexSmartSdrStubServer(
        int initialSliceCount = 2,
        bool rejectFullDuplex = false,
        bool rejectTxSlice = false,
        bool rejectSliceCreate = false,
        bool omitSliceCreateIndex = false,
        bool emitPartialSliceStatus = false,
        bool rejectClientProgram = false)
    {
        if (initialSliceCount >= 1)
            _slices[0] = new StubSlice(0, 145_900_000, "USB", Tx: false);
        if (initialSliceCount >= 2)
            _slices[1] = new StubSlice(1, 435_000_000, "USB", Tx: true);

        _nextSliceIndex = Math.Max(0, initialSliceCount);
        _rejectFullDuplex = rejectFullDuplex;
        _rejectTxSlice = rejectTxSlice;
        _rejectSliceCreate = rejectSliceCreate;
        _omitSliceCreateIndex = omitSliceCreateIndex;
        _emitPartialSliceStatus = emitPartialSliceStatus;
        _rejectClientProgram = rejectClientProgram;

        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _serverTask = Task.Run(RunServerAsync);
    }

    public int Port { get; }

    public bool FullDuplexEnabled
    {
        get
        {
            lock (_gate)
                return _fullDuplex;
        }
    }

    public IReadOnlyDictionary<int, StubSlice> Slices
    {
        get
        {
            lock (_gate)
                return new Dictionary<int, StubSlice>(_slices);
        }
    }

    public void WaitUntilReady(TimeSpan? timeout = null)
    {
        if (!_acceptLoopReady.Task.Wait(timeout ?? TimeSpan.FromSeconds(5)))
            throw new TimeoutException("Flex SmartSDR stub did not start accepting connections in time.");
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        try
        {
            _serverTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
        }

        _cts.Dispose();
    }

    private async Task RunServerAsync()
    {
        _acceptLoopReady.TrySetResult();
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false);
                _ = Task.Run(() => HandleClientAsync(client), _cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            await using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { AutoFlush = true };

            await writer.WriteLineAsync("V1.4.0.0").ConfigureAwait(false);
            await writer.WriteLineAsync("HABCDEF01").ConfigureAwait(false);
            await writer.WriteLineAsync("M10000001|Client connected").ConfigureAwait(false);
            await writer.WriteLineAsync(
                    "SABCDEF01|radio slices=2 panadapters=2 full_duplex_enabled=0 nickname=Stub")
                .ConfigureAwait(false);
            await EmitSlicesAsync(writer).ConfigureAwait(false);

            while (client.Connected && !_cts.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(_cts.Token).ConfigureAwait(false);
                if (line is null)
                    break;

                await HandleCommandAsync(line, writer).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            client.Dispose();
        }
    }

    private async Task HandleCommandAsync(string line, StreamWriter writer)
    {
        if (!line.StartsWith('C'))
            return;

        var bar = line.IndexOf('|');
        if (bar <= 1)
            return;

        var seqText = line[1..bar];
        if (seqText.StartsWith('D'))
            seqText = seqText[1..];

        if (!uint.TryParse(seqText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seq))
            return;

        var body = line[(bar + 1)..].Trim();

        if (body.StartsWith("client program", StringComparison.OrdinalIgnoreCase))
        {
            await writer.WriteLineAsync(
                    _rejectClientProgram
                        ? $"R{seq}|50000015|Client label unavailable"
                        : $"R{seq}|0|")
                .ConfigureAwait(false);
            return;
        }

        if (body.StartsWith("sub slice", StringComparison.OrdinalIgnoreCase)
            || body.StartsWith("sub radio", StringComparison.OrdinalIgnoreCase))
        {
            if (body.StartsWith("sub slice", StringComparison.OrdinalIgnoreCase))
                await EmitSlicesAsync(writer).ConfigureAwait(false);

            await writer.WriteLineAsync($"R{seq}|0|").ConfigureAwait(false);
            return;
        }

        if (body.StartsWith("radio set full_duplex_enabled=", StringComparison.OrdinalIgnoreCase))
        {
            var enabled = body.EndsWith("=1", StringComparison.Ordinal);
            if (enabled && _rejectFullDuplex)
            {
                await writer.WriteLineAsync($"R{seq}|50000015|FDX unavailable").ConfigureAwait(false);
                return;
            }

            lock (_gate)
                _fullDuplex = enabled;

            await writer.WriteLineAsync(
                    $"SABCDEF01|radio full_duplex_enabled={(enabled ? "1" : "0")}")
                .ConfigureAwait(false);
            await writer.WriteLineAsync($"R{seq}|0|").ConfigureAwait(false);
            return;
        }

        if (body.StartsWith("slice tune ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = body.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4
                && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                && double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz))
            {
                UpdateSlice(index, s => s with { FrequencyHz = FlexSmartSdrCodec.MhzToHz(mhz) });
                await EmitSliceAsync(writer, index).ConfigureAwait(false);
            }

            await writer.WriteLineAsync($"R{seq}|0|").ConfigureAwait(false);
            return;
        }

        if (body.StartsWith("slice set ", StringComparison.OrdinalIgnoreCase))
        {
            var match = Regex.Match(body, @"^slice set (\d+)\s+(.+)$", RegexOptions.IgnoreCase);
            if (match.Success
                && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                var args = match.Groups[2].Value;
                if (_rejectTxSlice && args.Contains("tx=1", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync($"R{seq}|50000015|TX slice unavailable").ConfigureAwait(false);
                    return;
                }

                ApplySliceSet(index, args);
                if (_emitPartialSliceStatus)
                {
                    await writer.WriteLineAsync($"SABCDEF01|slice {index} {args}").ConfigureAwait(false);
                }
                else
                {
                    await EmitSliceAsync(writer, index).ConfigureAwait(false);
                }
            }

            await writer.WriteLineAsync($"R{seq}|0|").ConfigureAwait(false);
            return;
        }

        if (body.StartsWith("slice create", StringComparison.OrdinalIgnoreCase))
        {
            if (_rejectSliceCreate)
            {
                await writer.WriteLineAsync($"R{seq}|50000015|Slice unavailable").ConfigureAwait(false);
                return;
            }

            var freq = 14.0;
            var mode = "USB";
            foreach (var token in body.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.StartsWith("freq=", StringComparison.OrdinalIgnoreCase)
                    && double.TryParse(token[5..], NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz))
                    freq = mhz;
                if (token.StartsWith("mode=", StringComparison.OrdinalIgnoreCase))
                    mode = token[5..];
            }

            int index;
            lock (_gate)
            {
                index = _nextSliceIndex++;
                _slices[index] = new StubSlice(index, FlexSmartSdrCodec.MhzToHz(freq), mode, Tx: false);
            }

            await EmitSliceAsync(writer, index).ConfigureAwait(false);
            await writer.WriteLineAsync(_omitSliceCreateIndex ? $"R{seq}|0|" : $"R{seq}|0|{index}")
                .ConfigureAwait(false);
            return;
        }

        await writer.WriteLineAsync($"R{seq}|0|").ConfigureAwait(false);
    }

    private void ApplySliceSet(int index, string args)
    {
        UpdateSlice(index, slice =>
        {
            var next = slice;
            foreach (var token in args.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = token.IndexOf('=');
                if (eq <= 0)
                    continue;

                var key = token[..eq];
                var value = token[(eq + 1)..];
                if (key.Equals("mode", StringComparison.OrdinalIgnoreCase))
                    next = next with { Mode = value };
                else if (key.Equals("tx", StringComparison.OrdinalIgnoreCase))
                    next = next with { Tx = value is "1" or "true" };
                else if (key.Equals("fm_tone_mode", StringComparison.OrdinalIgnoreCase))
                    next = next with { ToneMode = value };
                else if (key.Equals("fm_tone_value", StringComparison.OrdinalIgnoreCase)
                         && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var hz))
                    next = next with { ToneHz = hz };
            }

            return next;
        });

        // Only one TX slice
        if (_slices.TryGetValue(index, out var updated) && updated.Tx)
        {
            foreach (var key in _slices.Keys.ToArray())
            {
                if (key == index)
                    continue;
                UpdateSlice(key, s => s with { Tx = false });
            }
        }
    }

    private void UpdateSlice(int index, Func<StubSlice, StubSlice> update)
    {
        lock (_gate)
        {
            if (!_slices.TryGetValue(index, out var existing))
                existing = new StubSlice(index, 0, "USB", false);

            _slices[index] = update(existing);
        }
    }

    private async Task EmitSlicesAsync(StreamWriter writer)
    {
        foreach (var index in Slices.Keys.OrderBy(i => i))
            await EmitSliceAsync(writer, index).ConfigureAwait(false);
    }

    private async Task EmitSliceAsync(StreamWriter writer, int index)
    {
        if (!Slices.TryGetValue(index, out var slice))
            return;

        var mhz = FlexSmartSdrCodec.HzToMhz(slice.FrequencyHz).ToString("0.000000", CultureInfo.InvariantCulture);
        var tone = string.IsNullOrWhiteSpace(slice.ToneMode) ? "OFF" : slice.ToneMode;
        var toneHz = slice.ToneHz.ToString("0.0", CultureInfo.InvariantCulture);
        await writer.WriteLineAsync(
                $"SABCDEF01|slice {index} in_use=1 RF_frequency={mhz} mode={slice.Mode} tx={(slice.Tx ? "1" : "0")} active=0 fm_tone_mode={tone} fm_tone_value={toneHz}")
            .ConfigureAwait(false);
    }

    internal sealed record StubSlice(int Index, long FrequencyHz, string Mode, bool Tx, string ToneMode = "OFF", double ToneHz = 67.0);
}
