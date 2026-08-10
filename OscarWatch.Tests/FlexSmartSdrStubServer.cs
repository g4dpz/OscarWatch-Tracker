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
    private readonly object _writerGate = new();
    private StreamWriter? _connectedWriter;
    private int _nextSliceIndex;
    private bool _fullDuplex;
    private readonly bool _rejectFullDuplex;
    private readonly bool _rejectTxSlice;
    private readonly bool _rejectSliceCreate;
    private int _rejectSliceCreateRemaining;
    private readonly bool _omitSliceCreateIndex;
    private readonly bool _omitSliceCreateStatus;
    private readonly bool _emitGhostPartialSliceOnSubscribe;
    private readonly bool _emitPartialSliceStatus;
    private readonly bool _rejectClientProgram;
    private readonly bool _suppressSliceSetStatus;
    private readonly bool _omitPanOnCreateStatus;
    private readonly bool _silentCrossScuCenterReject;
    private readonly bool _allowUhfToVhfCenter;
    private readonly int? _maxPanCount;
    private readonly ConcurrentDictionary<string, long> _panCentersHz = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<string> _commandBodies = new();
    private string? _vhfPanStreamId;
    private string? _uhfPanStreamId;
    private int _nextPanSuffix;
    private int _activeSliceIndex;
    private int _nextCreatedPanId = 0x40000010;

    public FlexSmartSdrStubServer(
        int initialSliceCount = 2,
        bool rejectFullDuplex = false,
        bool rejectTxSlice = false,
        bool rejectSliceCreate = false,
        int rejectSliceCreateCount = 0,
        bool omitSliceCreateIndex = false,
        bool omitSliceCreateStatus = false,
        bool emitGhostPartialSliceOnSubscribe = false,
        bool emitPartialSliceStatus = false,
        bool rejectClientProgram = false,
        bool suppressSliceSetStatus = false,
        bool omitPanOnCreateStatus = false,
        bool silentCrossScuCenterReject = false,
        bool dualHfPanStartup = false,
        int? maxPanCount = null,
        bool allowUhfToVhfCenter = false)
    {
        if (dualHfPanStartup)
        {
            // Mark's "two different HF bands" start: both pans on VHF-group SCU, no UHF.
            _slices[0] = new StubSlice(0, 14_225_000, "USB", Tx: false, PanStreamId: "0x40000000");
            _slices[1] = new StubSlice(1, 21_275_000, "USB", Tx: true, PanStreamId: "0x40000001");
            _panCentersHz["0x40000000"] = 14_225_000;
            _panCentersHz["0x40000001"] = 21_275_000;
            _vhfPanStreamId = "0x40000000";
            _uhfPanStreamId = null;
            initialSliceCount = 2;
            maxPanCount ??= 2;
            silentCrossScuCenterReject = true;
        }
        else
        {
            if (initialSliceCount >= 1)
                _slices[0] = new StubSlice(0, 145_900_000, "USB", Tx: false, PanStreamId: "0x40000000");
            if (initialSliceCount >= 2)
                _slices[1] = new StubSlice(1, 435_000_000, "USB", Tx: true, PanStreamId: "0x40000001");

            if (initialSliceCount >= 2)
            {
                // Dual-pan startup: VHF + UHF (stream IDs match common SmartSDR layouts).
                _panCentersHz["0x40000000"] = 435_000_000;
                _panCentersHz["0x40000001"] = 145_900_000;
                _vhfPanStreamId = "0x40000001";
                _uhfPanStreamId = "0x40000000";
            }
            else if (initialSliceCount == 1)
            {
                // Single-pan startup (Mark's failure mode): only the VHF pan exists.
                _panCentersHz["0x40000000"] = 145_900_000;
                _vhfPanStreamId = "0x40000000";
                _uhfPanStreamId = null;
            }
        }

        _nextSliceIndex = Math.Max(0, initialSliceCount);
        _rejectFullDuplex = rejectFullDuplex;
        _rejectTxSlice = rejectTxSlice;
        _rejectSliceCreate = rejectSliceCreate;
        _rejectSliceCreateRemaining = Math.Max(0, rejectSliceCreateCount);
        _omitSliceCreateIndex = omitSliceCreateIndex;
        _omitSliceCreateStatus = omitSliceCreateStatus;
        _emitGhostPartialSliceOnSubscribe = emitGhostPartialSliceOnSubscribe;
        _emitPartialSliceStatus = emitPartialSliceStatus;
        _rejectClientProgram = rejectClientProgram;
        _suppressSliceSetStatus = suppressSliceSetStatus;
        _omitPanOnCreateStatus = omitPanOnCreateStatus;
        _silentCrossScuCenterReject = silentCrossScuCenterReject;
        _allowUhfToVhfCenter = allowUhfToVhfCenter;
        _maxPanCount = maxPanCount;
        _nextPanSuffix = Math.Max(0, initialSliceCount);

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

    public IReadOnlyDictionary<string, long> PanCentersHz =>
        new Dictionary<string, long>(_panCentersHz, StringComparer.OrdinalIgnoreCase);

    /// <summary>SmartSDR command bodies received (text after <c>Cseq|</c>), oldest first.</summary>
    public IReadOnlyList<string> CommandBodies => _commandBodies.ToArray();

    public void ClearCommandBodies()
    {
        while (_commandBodies.TryDequeue(out _))
        {
        }
    }

    public void WaitUntilReady(TimeSpan? timeout = null)
    {
        if (!_acceptLoopReady.Task.Wait(timeout ?? TimeSpan.FromSeconds(5)))
            throw new TimeoutException("Flex SmartSDR stub did not start accepting connections in time.");
    }

    public void SetSliceFrequencyFromOperator(int index, long hz)
    {
        UpdateSlice(index, slice => slice with { FrequencyHz = hz });
        var mhz = FlexSmartSdrCodec.HzToMhz(hz).ToString("0.000000", CultureInfo.InvariantCulture);
        lock (_writerGate)
        {
            if (_connectedWriter is null)
                throw new InvalidOperationException("No SmartSDR stub client is connected.");

            _connectedWriter.WriteLine($"SABCDEF01|slice {index} RF_frequency={mhz}");
            _connectedWriter.Flush();
        }
    }

    /// <summary>
    /// Moves one panadapter centre without a client command (on-band but off-slice cases).
    /// </summary>
    public void SetPanCenterFromOperator(string panStreamId, long centerHz)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(panStreamId);
        if (centerHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(centerHz));

        _panCentersHz[panStreamId] = centerHz;
        lock (_writerGate)
        {
            if (_connectedWriter is null)
                throw new InvalidOperationException("No SmartSDR stub client is connected.");

            var mhz = FlexSmartSdrCodec.HzToMhz(centerHz).ToString("0.######", CultureInfo.InvariantCulture);
            _connectedWriter.WriteLine($"SABCDEF01|display pan {panStreamId} center={mhz}");
            _connectedWriter.Flush();
        }
    }

    public int ActiveSliceIndex
    {
        get
        {
            lock (_gate)
                return _activeSliceIndex;
        }
    }

    /// <summary>
    /// Collapses both panadapters onto one band (Mark's pan-lock failure mode).
    /// </summary>
    public void CollapseBothPansOntoBand(bool ontoUhf)
    {
        var centerHz = ontoUhf ? 435_000_000L : 145_900_000L;
        foreach (var key in _panCentersHz.Keys.ToList())
            _panCentersHz[key] = centerHz;

        if (ontoUhf)
        {
            _uhfPanStreamId = _panCentersHz.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            _vhfPanStreamId = null;
        }
        else
        {
            _vhfPanStreamId = _panCentersHz.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            _uhfPanStreamId = null;
        }

        EmitAllPanCenters();
    }

    /// <summary>
    /// Swaps VHF/UHF pan centres in place (sticky lock lag after slice recreate).
    /// </summary>
    public void SwapPanBandCenters()
    {
        string? vhfId = null;
        string? uhfId = null;
        foreach (var entry in _panCentersHz)
        {
            if (RigSatModeHelper.IsVhfCenterKHz(entry.Value / 1000.0))
                vhfId ??= entry.Key;
            else if (RigSatModeHelper.IsUhfCenterKHz(entry.Value / 1000.0))
                uhfId ??= entry.Key;
        }

        if (vhfId is null || uhfId is null)
            throw new InvalidOperationException("SwapPanBandCenters requires live VHF and UHF pans.");

        var vhfHz = _panCentersHz[vhfId];
        var uhfHz = _panCentersHz[uhfId];
        _panCentersHz[vhfId] = uhfHz;
        _panCentersHz[uhfId] = vhfHz;
        _vhfPanStreamId = uhfId;
        _uhfPanStreamId = vhfId;
        EmitAllPanCenters();
    }

    private void EmitAllPanCenters()
    {
        lock (_writerGate)
        {
            if (_connectedWriter is null)
                return;

            foreach (var entry in _panCentersHz.OrderBy(static p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                var mhz = FlexSmartSdrCodec.HzToMhz(entry.Value).ToString("0.######", CultureInfo.InvariantCulture);
                _connectedWriter.WriteLine($"SABCDEF01|display pan {entry.Key} center={mhz}");
            }

            _connectedWriter.Flush();
        }
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
            lock (_writerGate)
                _connectedWriter = writer;

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
            lock (_writerGate)
            {
                if (ReferenceEquals(_connectedWriter, null) is false)
                    _connectedWriter = null;
            }
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
        _commandBodies.Enqueue(body);

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
            || body.StartsWith("sub radio", StringComparison.OrdinalIgnoreCase)
            || body.StartsWith("sub pan", StringComparison.OrdinalIgnoreCase))
        {
            if (body.StartsWith("sub slice", StringComparison.OrdinalIgnoreCase))
            {
                await EmitSlicesAsync(writer).ConfigureAwait(false);
                if (_emitGhostPartialSliceOnSubscribe)
                {
                    // Frequency/mode-only status without in_use must not invent a duplex peer.
                    await writer.WriteLineAsync("SABCDEF01|slice 9 RF_frequency=14.200000 mode=USB")
                        .ConfigureAwait(false);
                }
            }
            if (body.StartsWith("sub pan", StringComparison.OrdinalIgnoreCase))
                await EmitPansAsync(writer).ConfigureAwait(false);

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

        if (body.StartsWith("display pan set ", StringComparison.OrdinalIgnoreCase))
        {
            var match = Regex.Match(
                body,
                @"^display pan set (\S+)\s+center=([0-9.]+)",
                RegexOptions.IgnoreCase);
            if (match.Success
                && double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz))
            {
                var panId = match.Groups[1].Value;
                var centerHz = FlexSmartSdrCodec.MhzToHz(mhz);
                if (IsCrossScuPanCenter(panId, centerHz))
                {
                    // Real radios often allow UHF→VHF (collapse) while rejecting VHF→UHF.
                    if (_allowUhfToVhfCenter
                        && RigSatModeHelper.IsUhfCenterKHz(
                            _panCentersHz.TryGetValue(panId, out var liveHz) ? liveHz / 1000.0 : 0)
                        && RigSatModeHelper.IsVhfCenterKHz(centerHz / 1000.0))
                    {
                        // Fall through and apply the centre (models Mark's collapse path).
                    }
                    else if (_silentCrossScuCenterReject)
                    {
                        // Match Mark's radio: R|0| with no centre change (optimistic clients poison locks).
                        await writer.WriteLineAsync($"R{seq}|0|").ConfigureAwait(false);
                        return;
                    }
                    else
                    {
                        await writer.WriteLineAsync($"R{seq}|50000015|Pan centre band mismatch").ConfigureAwait(false);
                        return;
                    }
                }

                _panCentersHz[panId] = centerHz;
                if (RigSatModeHelper.IsVhfCenterKHz(centerHz / 1000.0))
                    _vhfPanStreamId = panId;
                else if (RigSatModeHelper.IsUhfCenterKHz(centerHz / 1000.0))
                    _uhfPanStreamId = panId;

                await writer.WriteLineAsync(
                        $"SABCDEF01|display pan {panId} center={mhz.ToString("0.######", CultureInfo.InvariantCulture)} autocenter=0")
                    .ConfigureAwait(false);
            }

            await writer.WriteLineAsync($"R{seq}|0|").ConfigureAwait(false);
            return;
        }

        if (body.Equals("display panafall create", StringComparison.OrdinalIgnoreCase)
            || body.Equals("panadapter create", StringComparison.OrdinalIgnoreCase))
        {
            if (_maxPanCount is int maxPans && _panCentersHz.Count >= maxPans)
            {
                await writer.WriteLineAsync($"R{seq}|50000007|").ConfigureAwait(false);
                return;
            }

            var panId = $"0x{Interlocked.Increment(ref _nextCreatedPanId) - 1:X8}";
            // New pans start on HF until centred (matches real radio defaults).
            _panCentersHz[panId] = 14_225_000;
            var mhz = FlexSmartSdrCodec.HzToMhz(14_225_000).ToString("0.######", CultureInfo.InvariantCulture);
            await writer.WriteLineAsync($"SABCDEF01|display pan {panId} center={mhz}").ConfigureAwait(false);
            await writer.WriteLineAsync($"R{seq}|0|pan={panId}").ConfigureAwait(false);
            return;
        }

        if (body.StartsWith("display pan remove ", StringComparison.OrdinalIgnoreCase)
            || body.StartsWith("display panafall remove ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = body.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4)
            {
                var panId = parts[^1];
                _panCentersHz.TryRemove(panId, out _);
                if (string.Equals(_vhfPanStreamId, panId, StringComparison.OrdinalIgnoreCase))
                    _vhfPanStreamId = null;
                if (string.Equals(_uhfPanStreamId, panId, StringComparison.OrdinalIgnoreCase))
                    _uhfPanStreamId = null;
            }

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

        if (body.StartsWith("slice m ", StringComparison.OrdinalIgnoreCase))
        {
            var match = Regex.Match(
                body,
                @"^slice m ([0-9.]+)\s+pan=(0x[0-9A-Fa-f]+)$",
                RegexOptions.IgnoreCase);
            if (match.Success
                && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz))
            {
                var panId = match.Groups[2].Value;
                int index;
                lock (_gate)
                    index = _activeSliceIndex;
                UpdateSlice(index, s => s with
                {
                    FrequencyHz = FlexSmartSdrCodec.MhzToHz(mhz),
                    PanStreamId = panId,
                });
                await EmitSliceAsync(writer, index).ConfigureAwait(false);
            }

            await writer.WriteLineAsync($"R{seq}|0|").ConfigureAwait(false);
            return;
        }

        if (body.StartsWith("slice set ", StringComparison.OrdinalIgnoreCase)
            || body.StartsWith("slice s ", StringComparison.OrdinalIgnoreCase))
        {
            var match = Regex.Match(body, @"^slice (?:set|s) (\d+)\s+(.+)$", RegexOptions.IgnoreCase);
            if (match.Success
                && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                var args = match.Groups[2].Value;
                if (args.Contains("pan=", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync($"R{seq}|5000002D|Bad field 'pan={ExtractPanField(args)}'")
                        .ConfigureAwait(false);
                    return;
                }

                if (args.Contains("active=1", StringComparison.OrdinalIgnoreCase))
                {
                    lock (_gate)
                        _activeSliceIndex = index;
                }
                else if (args.Contains("active=0", StringComparison.OrdinalIgnoreCase))
                {
                    lock (_gate)
                    {
                        if (_activeSliceIndex == index)
                            _activeSliceIndex = -1;
                    }
                }

                if (_rejectTxSlice && args.Contains("tx=1", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync($"R{seq}|50000015|TX slice unavailable").ConfigureAwait(false);
                    return;
                }

                ApplySliceSet(index, args);
                if (_suppressSliceSetStatus)
                {
                    // SmartSDR normally does not echo a change back to the client that issued it.
                }
                else if (_emitPartialSliceStatus)
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

        if (body.StartsWith("slice remove ", StringComparison.OrdinalIgnoreCase)
            || body.StartsWith("slice r ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = body.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3
                && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                lock (_gate)
                    _slices.TryRemove(index, out _);
                await writer.WriteLineAsync($"SABCDEF01|slice {index} in_use=0").ConfigureAwait(false);
            }

            await writer.WriteLineAsync($"R{seq}|0|").ConfigureAwait(false);
            return;
        }

        if (body.StartsWith("slice create", StringComparison.OrdinalIgnoreCase))
        {
            var rejectTemporarily = false;
            lock (_gate)
            {
                if (_rejectSliceCreateRemaining > 0)
                {
                    _rejectSliceCreateRemaining--;
                    rejectTemporarily = true;
                }
            }

            if (_rejectSliceCreate || rejectTemporarily)
            {
                await writer.WriteLineAsync($"R{seq}|50000015|Slice unavailable").ConfigureAwait(false);
                return;
            }

            var freq = 14.0;
            var mode = "USB";
            var ant = "";
            string? requestedPan = null;
            foreach (var token in body.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.StartsWith("freq=", StringComparison.OrdinalIgnoreCase)
                    && double.TryParse(token[5..], NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz))
                    freq = mhz;
                if (token.StartsWith("mode=", StringComparison.OrdinalIgnoreCase))
                    mode = token[5..];
                if (token.StartsWith("ant=", StringComparison.OrdinalIgnoreCase))
                    ant = token[4..];
                if (token.StartsWith("pan=", StringComparison.OrdinalIgnoreCase))
                    requestedPan = token[4..];
            }

            int index;
            string panId;
            var createdNewPan = false;
            var sliceHz = FlexSmartSdrCodec.MhzToHz(freq);
            var needsNewPan = string.IsNullOrWhiteSpace(requestedPan)
                || !_panCentersHz.ContainsKey(requestedPan);
            if (needsNewPan && _maxPanCount is int capacity && _panCentersHz.Count >= capacity)
            {
                // Match Mark's dual-HF capacity: cannot allocate a third pan / UHF slice.
                await writer.WriteLineAsync($"R{seq}|50000007|").ConfigureAwait(false);
                return;
            }

            lock (_gate)
            {
                index = _nextSliceIndex++;
                panId = !string.IsNullOrWhiteSpace(requestedPan)
                    ? requestedPan
                    : $"0x{(0x40000000 + _nextPanSuffix):X8}";
                if (string.IsNullOrWhiteSpace(requestedPan))
                    _nextPanSuffix++;
                _slices[index] = new StubSlice(
                    index,
                    sliceHz,
                    mode,
                    Tx: false,
                    RxAnt: ant,
                    PanStreamId: panId);
                if (!_panCentersHz.ContainsKey(panId))
                {
                    _panCentersHz[panId] = sliceHz;
                    createdNewPan = true;
                    if (RigSatModeHelper.IsVhfCenterKHz(sliceHz / 1000.0))
                        _vhfPanStreamId = panId;
                    else if (RigSatModeHelper.IsUhfCenterKHz(sliceHz / 1000.0))
                        _uhfPanStreamId = panId;
                }
                else if (!IsCrossScuPanCenter(panId, sliceHz))
                {
                    // Same-band create autopans the display (matches SmartSDR create behaviour).
                    _panCentersHz[panId] = sliceHz;
                }
            }

            if (createdNewPan || _panCentersHz.ContainsKey(panId))
            {
                var panMhz = FlexSmartSdrCodec.HzToMhz(_panCentersHz[panId])
                    .ToString("0.######", CultureInfo.InvariantCulture);
                await writer.WriteLineAsync($"SABCDEF01|display pan {panId} center={panMhz}")
                    .ConfigureAwait(false);
            }

            if (!_omitSliceCreateStatus)
                await EmitSliceAsync(writer, index, omitPan: _omitPanOnCreateStatus).ConfigureAwait(false);
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
                else if (key.Equals("rxant", StringComparison.OrdinalIgnoreCase))
                    next = next with { RxAnt = value };
                else if (key.Equals("txant", StringComparison.OrdinalIgnoreCase))
                    next = next with { TxAnt = value };
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

    private static string ExtractPanField(string args)
    {
        foreach (var token in args.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.StartsWith("pan=", StringComparison.OrdinalIgnoreCase))
                return token["pan=".Length..];
        }

        return string.Empty;
    }

    private bool IsCrossScuPanCenter(string panStreamId, long centerHz)
    {
        if (centerHz <= 0)
            return false;

        if (!_panCentersHz.TryGetValue(panStreamId, out var currentHz) || currentHz <= 0)
            return false;

        // Model Mark's radio: the VHF-group SCU (HF through 2 m) cannot jump to UHF and back.
        // IsVhfCenterKHz treats HF as VHF-group, so panafall create (HF default) cannot centre onto UHF.
        var currentIsUhf = RigSatModeHelper.IsUhfCenterKHz(currentHz / 1000.0);
        var targetIsUhf = RigSatModeHelper.IsUhfCenterKHz(centerHz / 1000.0);
        return currentIsUhf != targetIsUhf;
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

    private async Task EmitPansAsync(StreamWriter writer)
    {
        foreach (var entry in _panCentersHz.OrderBy(static p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            var mhz = FlexSmartSdrCodec.HzToMhz(entry.Value).ToString("0.######", CultureInfo.InvariantCulture);
            await writer.WriteLineAsync($"SABCDEF01|display pan {entry.Key} center={mhz}")
                .ConfigureAwait(false);
        }
    }

    private async Task EmitSliceAsync(StreamWriter writer, int index, bool omitPan = false)
    {
        if (!Slices.TryGetValue(index, out var slice))
            return;

        var mhz = FlexSmartSdrCodec.HzToMhz(slice.FrequencyHz).ToString("0.000000", CultureInfo.InvariantCulture);
        var tone = string.IsNullOrWhiteSpace(slice.ToneMode) ? "OFF" : slice.ToneMode;
        var toneHz = slice.ToneHz.ToString("0.0", CultureInfo.InvariantCulture);
        var panField = "";
        if (!omitPan)
        {
            var pan = string.IsNullOrWhiteSpace(slice.PanStreamId)
                ? $"0x{(0x40000000 + index):X8}"
                : slice.PanStreamId;
            panField = $" pan={pan}";
        }

        await writer.WriteLineAsync(
                $"SABCDEF01|slice {index} in_use=1 RF_frequency={mhz} mode={slice.Mode} tx={(slice.Tx ? "1" : "0")} active={(index == _activeSliceIndex ? "1" : "0")}{panField} fm_tone_mode={tone} fm_tone_value={toneHz}")
            .ConfigureAwait(false);
    }

    internal sealed record StubSlice(
        int Index,
        long FrequencyHz,
        string Mode,
        bool Tx,
        string ToneMode = "OFF",
        double ToneHz = 67.0,
        string RxAnt = "",
        string TxAnt = "",
        string PanStreamId = "");
}
