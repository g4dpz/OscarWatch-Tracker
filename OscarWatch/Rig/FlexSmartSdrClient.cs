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
    private readonly Dictionary<string, FlexPanState> _pans = new(StringComparer.OrdinalIgnoreCase);
    private string? _lockedVhfPanStreamId;
    private string? _lockedUhfPanStreamId;
    private readonly Dictionary<int, long> _sliceFrequencyRevisions = new();
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
            Log.Information("Connecting to FlexRadio SmartSDR at {Host}:{Port}", _host, _port);
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
            _pans.Clear();
            ClearLockedBandPansUnlocked();
            _nextSequence = 1;

            ReadPrologueUnlocked();
            if (!SendAndWaitUnlocked(seq => FlexSmartSdrCodec.BuildClientProgramCommand(seq)))
            {
                Log.Warning(
                    "Flex SmartSDR did not acknowledge the optional OscarWatch client label; continuing connection");
            }
            RequireCommandUnlocked(
                seq => FlexSmartSdrCodec.BuildSubSliceAllCommand(seq),
                "subscribe to slice status");
            RequireCommandUnlocked(
                seq => FlexSmartSdrCodec.BuildSubRadioAllCommand(seq),
                "subscribe to radio status");
            RequireCommandUnlocked(
                seq => FlexSmartSdrCodec.BuildSubPanAllCommand(seq),
                "subscribe to panadapter status");
            DrainPendingStatusUnlocked();
            Log.Information(
                "Connected to FlexRadio SmartSDR at {Host}:{Port}; version={Version}, handle={Handle}, slices={SliceCount}",
                _host,
                _port,
                _version,
                _handle,
                _slices.Values.Count(s => s.InUse));
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

    public bool TuneSliceIfStatusUnchanged(
        int sliceIndex,
        long hz,
        long expectedRevision,
        long expectedFrequencyHz)
    {
        lock (_gate)
        {
            EnsureConnectedUnlocked();
            DrainPendingStatusUnlocked();
            var currentRevision = _sliceFrequencyRevisions.GetValueOrDefault(sliceIndex);
            if (currentRevision != expectedRevision
                && _slices.TryGetValue(sliceIndex, out var current)
                && current.FrequencyHz != expectedFrequencyHz
                && current.FrequencyHz != hz)
            {
                Log.Debug(
                    "Skipped stale FlexRadio RX tune: slice={SliceIndex}, requestedHz={RequestedHz}, currentHz={CurrentHz}, expectedRevision={ExpectedRevision}, currentRevision={CurrentRevision}",
                    sliceIndex,
                    hz,
                    current.FrequencyHz,
                    expectedRevision,
                    currentRevision);
                return false;
            }

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
                var slice = _slices[key];
                if (key == sliceIndex)
                    _slices[key] = slice with { IsTransmit = tx };
                else if (tx && slice.IsTransmit)
                    _slices[key] = slice with { IsTransmit = false };
            }

            return true;
        }
    }

    public bool SetSliceRxAnt(int sliceIndex, string antennaPort, out string? failureDetail)
    {
        lock (_gate)
        {
            EnsureConnectedUnlocked();
            return TrySendSliceCommandUnlocked(
                seq => FlexSmartSdrCodec.BuildSliceSetRxAntCommand(seq, sliceIndex, antennaPort),
                out failureDetail);
        }
    }

    public bool SetSliceTxAnt(int sliceIndex, string antennaPort, out string? failureDetail)
    {
        lock (_gate)
        {
            EnsureConnectedUnlocked();
            return TrySendSliceCommandUnlocked(
                seq => FlexSmartSdrCodec.BuildSliceSetTxAntCommand(seq, sliceIndex, antennaPort),
                out failureDetail);
        }
    }

    public string? GetSlicePanStreamId(int sliceIndex)
    {
        lock (_gate)
        {
            DrainPendingStatusUnlocked();
            return _slices.TryGetValue(sliceIndex, out var slice)
                   && !string.IsNullOrWhiteSpace(slice.PanStreamId)
                ? slice.PanStreamId
                : null;
        }
    }

    public long? GetPanCenterHz(string panStreamId)
    {
        lock (_gate)
        {
            EnsureConnectedUnlocked();
            DrainPendingStatusUnlocked();
            if (string.IsNullOrWhiteSpace(panStreamId))
                return null;

            return _pans.TryGetValue(panStreamId, out var pan) && pan.CenterHz > 0
                ? pan.CenterHz
                : null;
        }
    }

    /// <summary>Drops sticky VHF/UHF pan locks so the next bind/centre re-resolves from live pan centres.</summary>
    public void ClearLockedBandPans()
    {
        lock (_gate)
            ClearLockedBandPansUnlocked();
    }

    /// <summary>
    /// Re-resolves VHF/UHF pan locks from live centres only when both bands are present.
    /// Clears locks whose live centres no longer match their band. When still collapsed,
    /// keeps any remaining sticky lock only as a hint for recovery (does not invent bands).
    /// </summary>
    public bool TryRelockBandPansFromLiveCentres()
    {
        lock (_gate)
        {
            EnsureConnectedUnlocked();
            DrainPendingStatusUnlocked();
            InvalidateStaleBandPanLocksUnlocked();

            if (!FlexPanBandResolver.TryResolveBandPans(_pans.Values, out var vhfPan, out var uhfPan)
                || string.IsNullOrWhiteSpace(vhfPan)
                || string.IsNullOrWhiteSpace(uhfPan)
                || !_pans.ContainsKey(vhfPan)
                || !_pans.ContainsKey(uhfPan))
            {
                Log.Warning(
                    "FlexRadio cannot relock band pans from live centres; vhfPan={VhfPan}, uhfPan={UhfPan}, stickyVhf={StickyVhf}, stickyUhf={StickyUhf}",
                    vhfPan ?? "(missing)",
                    uhfPan ?? "(missing)",
                    _lockedVhfPanStreamId ?? "(none)",
                    _lockedUhfPanStreamId ?? "(none)");
                return false;
            }

            _lockedVhfPanStreamId = vhfPan;
            _lockedUhfPanStreamId = uhfPan;
            return true;
        }
    }

    /// <summary>
    /// Ensures separate live VHF and UHF panadapters exist and are locked.
    /// When both pans have collapsed onto one band, recentres a candidate pan or creates a
    /// new panafall on the missing band (AetherSDR-validated SmartSDR commands).
    /// </summary>
    public bool EnsureDualBandPanLayout(long vhfHz, long uhfHz)
    {
        lock (_gate)
        {
            EnsureConnectedUnlocked();
            DrainPendingStatusUnlocked();
            InvalidateStaleBandPanLocksUnlocked();

            if (TryLockLiveDualBandPansUnlocked())
                return true;

            if (vhfHz <= 0 || uhfHz <= 0)
            {
                Log.Warning(
                    "FlexRadio dual-band pan recovery needs VHF and UHF targets; vhfHz={VhfHz}, uhfHz={UhfHz}",
                    vhfHz,
                    uhfHz);
                return false;
            }

            Log.Warning(
                "FlexRadio dual-band pans missing or collapsed; attempting recovery: vhfPan={VhfPan}, uhfPan={UhfPan}",
                _lockedVhfPanStreamId ?? "(missing)",
                _lockedUhfPanStreamId ?? "(missing)");

            RemoveAllInUseSlicesUnlocked();

            FlexPanBandResolver.TryResolveBandPans(_pans.Values, out var liveVhf, out var liveUhf);
            var missingVhf = string.IsNullOrWhiteSpace(liveVhf);
            var missingUhf = string.IsNullOrWhiteSpace(liveUhf);

            if (missingVhf)
                TryRecoverMissingBandPanUnlocked(vhfHz, isVhf: true, keepPanStreamId: liveUhf);
            if (missingUhf)
                TryRecoverMissingBandPanUnlocked(uhfHz, isVhf: false, keepPanStreamId: liveVhf);

            DrainPendingStatusUnlocked();
            if (TryLockLiveDualBandPansUnlocked())
            {
                Log.Information(
                    "FlexRadio dual-band pan layout restored: vhfPan={VhfPan}, uhfPan={UhfPan}",
                    _lockedVhfPanStreamId,
                    _lockedUhfPanStreamId);
                return true;
            }

            Log.Warning(
                "FlexRadio dual-band pan recovery failed; vhfPan={VhfPan}, uhfPan={UhfPan}",
                _lockedVhfPanStreamId ?? "(missing)",
                _lockedUhfPanStreamId ?? "(missing)");
            return false;
        }
    }

    public void GetLockedBandPanStreamIds(out string? vhfPanStreamId, out string? uhfPanStreamId)
    {
        lock (_gate)
        {
            vhfPanStreamId = _lockedVhfPanStreamId;
            uhfPanStreamId = _lockedUhfPanStreamId;
        }
    }

    /// <summary>
    /// After drain, checks RX/TX slice frequencies, TX roles, distinct locked pans, pan bands, and optional modes.
    /// </summary>
    public bool VerifyDuplexSliceFrequencies(
        int rxSliceIndex,
        int txSliceIndex,
        long downlinkHz,
        long uplinkHz,
        bool satelliteMode,
        out string detail,
        string? expectedRxMode = null,
        string? expectedTxMode = null)
    {
        lock (_gate)
        {
            EnsureConnectedUnlocked();
            DrainPendingStatusUnlocked();
            InvalidateStaleBandPanLocksUnlocked();
            EnsureLockedBandPansUnlocked();

            if (downlinkHz > 0)
            {
                if (!_slices.TryGetValue(rxSliceIndex, out var rx) || !rx.InUse || rx.FrequencyHz <= 0)
                {
                    detail = $"RX slice {rxSliceIndex} missing or has no frequency";
                    return false;
                }

                if (rx.IsTransmit)
                {
                    detail = $"RX slice {rxSliceIndex} is marked TX";
                    return false;
                }

                if (!FrequenciesNearlyEqual(rx.FrequencyHz, downlinkHz))
                {
                    detail =
                        $"RX slice {rxSliceIndex} at {rx.FrequencyHz} Hz, expected {downlinkHz} Hz";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(expectedRxMode)
                    && !string.Equals(rx.Mode, expectedRxMode, StringComparison.OrdinalIgnoreCase))
                {
                    detail =
                        $"RX slice {rxSliceIndex} mode {rx.Mode}, expected {expectedRxMode}";
                    return false;
                }

                var expectedRxPan = ResolveLockedPanForFrequencyUnlocked(downlinkHz);
                if (!string.IsNullOrWhiteSpace(expectedRxPan)
                    && !string.Equals(rx.PanStreamId, expectedRxPan, StringComparison.OrdinalIgnoreCase))
                {
                    detail =
                        $"RX slice {rxSliceIndex} on pan {rx.PanStreamId ?? "(none)"}, expected {expectedRxPan}";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(rx.PanStreamId)
                    && _pans.TryGetValue(rx.PanStreamId, out var rxPan)
                    && rxPan.CenterHz > 0
                    && !PanCenterMatchesFrequencyBand(rxPan.CenterHz, downlinkHz))
                {
                    detail =
                        $"RX pan {rx.PanStreamId} centre {rxPan.CenterHz} Hz is not on the downlink band ({downlinkHz} Hz)";
                    return false;
                }
            }

            if (satelliteMode && uplinkHz > 0)
            {
                if (!_slices.TryGetValue(txSliceIndex, out var tx) || !tx.InUse || tx.FrequencyHz <= 0)
                {
                    detail = $"TX slice {txSliceIndex} missing or has no frequency";
                    return false;
                }

                if (!tx.IsTransmit)
                {
                    detail = $"TX slice {txSliceIndex} is not marked TX";
                    return false;
                }

                if (rxSliceIndex == txSliceIndex)
                {
                    detail = $"RX and TX share slice index {rxSliceIndex}";
                    return false;
                }

                if (!FrequenciesNearlyEqual(tx.FrequencyHz, uplinkHz))
                {
                    detail =
                        $"TX slice {txSliceIndex} at {tx.FrequencyHz} Hz, expected {uplinkHz} Hz";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(expectedTxMode)
                    && !string.Equals(tx.Mode, expectedTxMode, StringComparison.OrdinalIgnoreCase))
                {
                    detail =
                        $"TX slice {txSliceIndex} mode {tx.Mode}, expected {expectedTxMode}";
                    return false;
                }

                var expectedTxPan = ResolveLockedPanForFrequencyUnlocked(uplinkHz);
                if (!string.IsNullOrWhiteSpace(expectedTxPan)
                    && !string.Equals(tx.PanStreamId, expectedTxPan, StringComparison.OrdinalIgnoreCase))
                {
                    detail =
                        $"TX slice {txSliceIndex} on pan {tx.PanStreamId ?? "(none)"}, expected {expectedTxPan}";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(tx.PanStreamId)
                    && _pans.TryGetValue(tx.PanStreamId, out var txPan)
                    && txPan.CenterHz > 0
                    && !PanCenterMatchesFrequencyBand(txPan.CenterHz, uplinkHz))
                {
                    detail =
                        $"TX pan {tx.PanStreamId} centre {txPan.CenterHz} Hz is not on the uplink band ({uplinkHz} Hz)";
                    return false;
                }

                if (downlinkHz > 0
                    && _slices.TryGetValue(rxSliceIndex, out var rxForPan)
                    && !string.IsNullOrWhiteSpace(rxForPan.PanStreamId)
                    && !string.IsNullOrWhiteSpace(tx.PanStreamId)
                    && string.Equals(rxForPan.PanStreamId, tx.PanStreamId, StringComparison.OrdinalIgnoreCase))
                {
                    detail = $"RX and TX slices share pan {tx.PanStreamId}";
                    return false;
                }
            }

            detail = "";
            return true;
        }
    }

    internal static bool FrequenciesNearlyEqual(long actualHz, long expectedHz, long toleranceHz = 1000) =>
        Math.Abs(actualHz - expectedHz) <= toleranceHz;

    private static bool PanCenterMatchesFrequencyBand(long panCenterHz, long frequencyHz)
    {
        var panKHz = panCenterHz / 1000.0;
        var freqKHz = frequencyHz / 1000.0;
        if (RigSatModeHelper.IsVhfCenterKHz(freqKHz))
            return RigSatModeHelper.IsVhfCenterKHz(panKHz);
        if (RigSatModeHelper.IsUhfCenterKHz(freqKHz))
            return RigSatModeHelper.IsUhfCenterKHz(panKHz);
        return true;
    }

    public bool SetPanCenter(string panStreamId, long centerHz, out string? failureDetail)
    {
        lock (_gate)
        {
            EnsureConnectedUnlocked();
            if (string.IsNullOrWhiteSpace(panStreamId) || centerHz <= 0)
            {
                failureDetail = "missing pan stream id or centre frequency";
                return false;
            }

            return SetPanCenterUnlocked(panStreamId, centerHz, out failureDetail);
        }
    }

    /// <summary>
    /// Ensures RX/TX slices live on the locked VHF/UHF panadapters for the pass frequencies.
    /// When either slice is on the wrong pan (typical V/U↔U/V swap), both are removed and
    /// recreated with <c>slice create … pan=</c> — <c>slice m</c> cannot safely swap two
    /// occupied pans, and <c>slice set … pan=</c> is rejected by SmartSDR.
    /// </summary>
    /// <param name="forceRebind">
    /// When true (e.g. V/U↔U/V layout flip), skip the cache short-circuit and recreate.
    /// </param>
    public bool BindDuplexSlicesToBandPans(
        ref int rxSliceIndex,
        ref int txSliceIndex,
        long downlinkHz,
        long uplinkHz,
        bool satelliteMode,
        bool forceRebind = false)
    {
        lock (_gate)
        {
            EnsureConnectedUnlocked();
            DrainPendingStatusUnlocked();
            InvalidateStaleBandPanLocksUnlocked();
            EnsureLockedBandPansUnlocked();
            ResolveDuplexSliceRolesUnlocked(ref rxSliceIndex, ref txSliceIndex);

            if (satelliteMode && downlinkHz > 0 && uplinkHz > 0
                && (string.IsNullOrWhiteSpace(_lockedVhfPanStreamId)
                    || string.IsNullOrWhiteSpace(_lockedUhfPanStreamId)))
            {
                Log.Warning(
                    "FlexRadio duplex pass requires separate VHF and UHF panadapters; vhfPan={VhfPan}, uhfPan={UhfPan}",
                    _lockedVhfPanStreamId ?? "(missing)",
                    _lockedUhfPanStreamId ?? "(missing)");
                return false;
            }

            if (downlinkHz <= 0)
                return true;

            var downlinkPan = ResolveLockedPanForFrequencyUnlocked(downlinkHz);
            if (string.IsNullOrWhiteSpace(downlinkPan))
            {
                Log.Warning(
                    "FlexRadio could not resolve downlink pan for bind: downlinkHz={DownlinkHz}",
                    downlinkHz);
                return false;
            }

            string? uplinkPan = null;
            if (satelliteMode && uplinkHz > 0)
            {
                uplinkPan = ResolveLockedPanForFrequencyUnlocked(uplinkHz);
                if (string.IsNullOrWhiteSpace(uplinkPan))
                {
                    Log.Warning(
                        "FlexRadio could not resolve uplink pan for bind: uplinkHz={UplinkHz}",
                        uplinkHz);
                    return false;
                }
            }

            if (!forceRebind
                && DuplexSlicesAlreadyBoundUnlocked(
                    rxSliceIndex,
                    txSliceIndex,
                    downlinkPan,
                    uplinkPan,
                    satelliteMode,
                    uplinkHz))
            {
                return true;
            }

            _slices.TryGetValue(rxSliceIndex, out var rxSlice);
            var rxMode = rxSlice?.Mode;
            if (string.IsNullOrWhiteSpace(rxMode))
                rxMode = "USB";
            var txMode = _slices.TryGetValue(txSliceIndex, out var existingTx) ? existingTx.Mode : null;
            if (string.IsNullOrWhiteSpace(txMode))
                txMode = "USB";

            // Remove TX first so the target pan is free, then RX, then recreate on locked pans.
            if (_slices.TryGetValue(txSliceIndex, out var txInUse) && txInUse.InUse)
            {
                if (!RemoveSliceUnlocked(txSliceIndex, out var removeTxFailure))
                {
                    Log.Warning(
                        "FlexRadio failed to remove TX slice {SliceIndex} for pan rebind: detail={Detail}",
                        txSliceIndex,
                        removeTxFailure);
                    return false;
                }
            }

            if (_slices.TryGetValue(rxSliceIndex, out var rxInUse) && rxInUse.InUse)
            {
                if (!RemoveSliceUnlocked(rxSliceIndex, out var removeRxFailure))
                {
                    Log.Warning(
                        "FlexRadio failed to remove RX slice {SliceIndex} for pan rebind: detail={Detail}",
                        rxSliceIndex,
                        removeRxFailure);
                    return false;
                }
            }

            var newRx = CreateSliceUnlocked(downlinkHz, rxMode, ant: null, downlinkPan);
            if (newRx is null)
            {
                Log.Warning(
                    "FlexRadio failed to recreate RX slice on pan {PanStreamId}: downlinkHz={DownlinkHz}",
                    downlinkPan,
                    downlinkHz);
                return false;
            }

            if (!VerifySlicePanUnlocked(newRx.Value, downlinkPan))
            {
                Log.Warning(
                    "FlexRadio RX slice {SliceIndex} did not attach to pan {PanStreamId} after create",
                    newRx.Value,
                    downlinkPan);
                return false;
            }

            rxSliceIndex = newRx.Value;

            if (satelliteMode && uplinkHz > 0 && !string.IsNullOrWhiteSpace(uplinkPan))
            {
                var newTx = CreateSliceUnlocked(uplinkHz, txMode, ant: null, uplinkPan);
                if (newTx is null)
                {
                    Log.Warning(
                        "FlexRadio failed to recreate TX slice on pan {PanStreamId}: uplinkHz={UplinkHz}",
                        uplinkPan,
                        uplinkHz);
                    return false;
                }

                if (!VerifySlicePanUnlocked(newTx.Value, uplinkPan))
                {
                    Log.Warning(
                        "FlexRadio TX slice {SliceIndex} did not attach to pan {PanStreamId} after create",
                        newTx.Value,
                        uplinkPan);
                    return false;
                }

                txSliceIndex = newTx.Value;
                var txIndex = txSliceIndex;
                if (!SendAndWaitUnlocked(seq => FlexSmartSdrCodec.BuildSliceSetTxCommand(seq, txIndex, tx: true)))
                {
                    Log.Warning(
                        "FlexRadio failed to mark recreated slice {SliceIndex} as TX after pan rebind",
                        txIndex);
                    return false;
                }

                DrainPendingStatusUnlocked();
                if (_slices.TryGetValue(txIndex, out var createdTx))
                    _slices[txIndex] = createdTx with { IsTransmit = true };
                if (_slices.TryGetValue(rxSliceIndex, out var createdRx))
                    _slices[rxSliceIndex] = createdRx with { IsTransmit = false };

                if (!DuplexSlicesAlreadyBoundUnlocked(
                        rxSliceIndex,
                        txSliceIndex,
                        downlinkPan,
                        uplinkPan,
                        satelliteMode,
                        uplinkHz))
                {
                    Log.Warning(
                        "FlexRadio pan rebind verification failed after recreate: rxSlice={RxSlice} pan={RxPan}, txSlice={TxSlice} pan={TxPan}",
                        rxSliceIndex,
                        downlinkPan,
                        txSliceIndex,
                        uplinkPan);
                    return false;
                }
            }

            Log.Information(
                "FlexRadio rebound duplex slices onto band pans: rxSlice={RxSlice} pan={RxPan}, txSlice={TxSlice} pan={TxPan}, forceRebind={ForceRebind}",
                rxSliceIndex,
                downlinkPan,
                txSliceIndex,
                uplinkPan ?? "(none)",
                forceRebind);
            return true;
        }
    }

    /// <summary>Re-reads in-use slices and assigns RX/TX indices from the live <c>tx=1</c> flag.</summary>
    public void ResolveDuplexSliceRoles(ref int rxSliceIndex, ref int txSliceIndex)
    {
        lock (_gate)
        {
            EnsureConnectedUnlocked();
            DrainPendingStatusUnlocked();
            ResolveDuplexSliceRolesUnlocked(ref rxSliceIndex, ref txSliceIndex);
        }
    }

    public bool CenterBandPans(long downlinkHz, long uplinkHz, bool satelliteMode)
    {
        lock (_gate)
        {
            EnsureConnectedUnlocked();
            DrainPendingStatusUnlocked();
            InvalidateStaleBandPanLocksUnlocked();
            EnsureLockedBandPansUnlocked();

            FlexPanBandResolver.ResolveTargetFrequencies(
                downlinkHz,
                uplinkHz,
                satelliteMode,
                out var vhfHz,
                out var uhfHz);

            if (satelliteMode && downlinkHz > 0 && uplinkHz > 0
                && (string.IsNullOrWhiteSpace(_lockedVhfPanStreamId)
                    || string.IsNullOrWhiteSpace(_lockedUhfPanStreamId)))
            {
                Log.Warning(
                    "FlexRadio duplex pass requires separate VHF and UHF panadapters for centre; vhfPan={VhfPan}, uhfPan={UhfPan}",
                    _lockedVhfPanStreamId ?? "(missing)",
                    _lockedUhfPanStreamId ?? "(missing)");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(_lockedVhfPanStreamId)
                || !string.IsNullOrWhiteSpace(_lockedUhfPanStreamId))
            {
                var ok = true;
                if (vhfHz > 0 && !string.IsNullOrWhiteSpace(_lockedVhfPanStreamId))
                {
                    ok &= SetPanCenterUnlocked(_lockedVhfPanStreamId, vhfHz, out var vhfFailure);
                    if (!ok)
                    {
                        Log.Warning(
                            "FlexRadio failed to centre VHF pan {PanStreamId}: centerHz={CenterHz}, detail={Detail}",
                            _lockedVhfPanStreamId,
                            vhfHz,
                            vhfFailure);
                    }
                }

                if (uhfHz > 0 && !string.IsNullOrWhiteSpace(_lockedUhfPanStreamId))
                {
                    var uhfOk = SetPanCenterUnlocked(_lockedUhfPanStreamId, uhfHz, out var uhfFailure);
                    ok &= uhfOk;
                    if (!uhfOk)
                    {
                        Log.Warning(
                            "FlexRadio failed to centre UHF pan {PanStreamId}: centerHz={CenterHz}, detail={Detail}",
                            _lockedUhfPanStreamId,
                            uhfHz,
                            uhfFailure);
                    }
                }

                return ok;
            }

            return CenterBandPansBySliceAssociationUnlocked(downlinkHz, uplinkHz, satelliteMode);
        }
    }

    private bool CenterBandPansBySliceAssociationUnlocked(long downlinkHz, long uplinkHz, bool satelliteMode)
    {
        var ok = true;
        if (downlinkHz > 0)
            ok &= TryCenterSlicePanUnlocked(FindRxSliceIndexUnlocked(), downlinkHz);

        if (satelliteMode && uplinkHz > 0)
            ok &= TryCenterSlicePanUnlocked(FindTxSliceIndexUnlocked(), uplinkHz);

        return ok;
    }

    private int FindRxSliceIndexUnlocked()
    {
        var tx = _slices.Values.FirstOrDefault(s => s.InUse && s.IsTransmit);
        if (tx is not null)
        {
            var rx = _slices.Values.FirstOrDefault(s => s.InUse && s.Index != tx.Index);
            if (rx is not null)
                return rx.Index;
        }

        return _slices.Values.Where(s => s.InUse).Select(s => s.Index).DefaultIfEmpty(0).Min();
    }

    private int FindTxSliceIndexUnlocked()
    {
        var tx = _slices.Values.FirstOrDefault(s => s.InUse && s.IsTransmit);
        if (tx is not null)
            return tx.Index;

        return _slices.Values.Where(s => s.InUse).Select(s => s.Index).DefaultIfEmpty(1).Max();
    }

    private bool TryCenterSlicePanUnlocked(int sliceIndex, long centerHz)
    {
        if (!_slices.TryGetValue(sliceIndex, out var slice) || string.IsNullOrWhiteSpace(slice.PanStreamId))
            return false;

        return SetPanCenterUnlocked(slice.PanStreamId, centerHz, out _);
    }

    private void ResolveDuplexSliceRolesUnlocked(ref int rxSliceIndex, ref int txSliceIndex)
    {
        var inUse = _slices.Values.Where(s => s.InUse).OrderBy(s => s.Index).ToList();
        if (inUse.Count < 2)
            return;

        var tx = inUse.FirstOrDefault(s => s.IsTransmit);
        if (tx is not null)
        {
            txSliceIndex = tx.Index;
            rxSliceIndex = inUse.FirstOrDefault(s => s.Index != tx.Index)?.Index ?? (tx.Index == 0 ? 1 : 0);
            return;
        }

        // Prefer caller's indices when both still exist; otherwise fall back to lowest/highest.
        var rxIndex = rxSliceIndex;
        var txIndex = txSliceIndex;
        if (inUse.Any(s => s.Index == rxIndex) && inUse.Any(s => s.Index == txIndex)
            && rxIndex != txIndex)
        {
            return;
        }

        rxSliceIndex = inUse[0].Index;
        txSliceIndex = inUse[1].Index;
    }

    private bool DuplexSlicesAlreadyBoundUnlocked(
        int rxSliceIndex,
        int txSliceIndex,
        string downlinkPan,
        string? uplinkPan,
        bool satelliteMode,
        long uplinkHz)
    {
        if (!_slices.TryGetValue(rxSliceIndex, out var rxSlice)
            || !rxSlice.InUse
            || string.IsNullOrWhiteSpace(rxSlice.PanStreamId)
            || !string.Equals(rxSlice.PanStreamId, downlinkPan, StringComparison.OrdinalIgnoreCase)
            || rxSlice.IsTransmit)
        {
            return false;
        }

        if (!satelliteMode || uplinkHz <= 0)
            return true;

        if (string.IsNullOrWhiteSpace(uplinkPan)
            || !_slices.TryGetValue(txSliceIndex, out var txSlice)
            || !txSlice.InUse
            || string.IsNullOrWhiteSpace(txSlice.PanStreamId)
            || !string.Equals(txSlice.PanStreamId, uplinkPan, StringComparison.OrdinalIgnoreCase)
            || !txSlice.IsTransmit)
        {
            return false;
        }

        return rxSliceIndex != txSliceIndex;
    }

    private bool VerifySlicePanUnlocked(int sliceIndex, string expectedPanStreamId)
    {
        DrainPendingStatusUnlocked();
        return _slices.TryGetValue(sliceIndex, out var slice)
            && slice.InUse
            && !string.IsNullOrWhiteSpace(slice.PanStreamId)
            && string.Equals(slice.PanStreamId, expectedPanStreamId, StringComparison.OrdinalIgnoreCase);
    }

    private bool RemoveSliceUnlocked(int sliceIndex, out string? failureDetail)
    {
        var ok = TrySendSliceCommandUnlocked(
            seq => FlexSmartSdrCodec.BuildSliceRemoveCommand(seq, sliceIndex),
            out failureDetail);
        if (ok)
        {
            _slices.Remove(sliceIndex);
            _sliceFrequencyRevisions.Remove(sliceIndex);
        }

        return ok;
    }

    private int? CreateSliceUnlocked(long hz, string mode, string? ant, string? panStreamId)
    {
        var existingIndexes = _slices.Keys.ToHashSet();
        var mhz = FlexSmartSdrCodec.HzToMhz(hz);
        var response = SendAndWaitResponseUnlocked(seq =>
            FlexSmartSdrCodec.BuildSliceCreateCommand(seq, mhz, mode, ant, panStreamId));
        if (response is null)
        {
            Log.Warning(
                "FlexRadio slice create timed out: freqHz={FrequencyHz}, mode={Mode}, pan={PanStreamId}, ant={Ant}",
                hz,
                mode,
                panStreamId ?? "(none)",
                ant ?? "(none)");
            return null;
        }

        if (!FlexSmartSdrCodec.IsSuccessResponse(response))
        {
            Log.Warning(
                "FlexRadio slice create failed: detail=hex=0x{Hex:X8}, body={Body}, freqHz={FrequencyHz}, mode={Mode}, pan={PanStreamId}, ant={Ant}",
                response.HexResponse,
                TruncateForLog(response.Body),
                hz,
                mode,
                panStreamId ?? "(none)",
                ant ?? "(none)");
            return null;
        }

        int? created;
        if (FlexSmartSdrCodec.TryParseSliceCreateIndex(response.Body, out var index))
        {
            // Trust the successful R body even when MultiFlex status lags behind the create ack.
            SeedCreatedSliceUnlocked(index, hz, mode, panStreamId);
            created = index;
            DrainPendingStatusUnlocked();
        }
        else
        {
            created = WaitForCreatedSliceUnlocked(existingIndexes, responseIndex: null);
            if (created is null)
            {
                Log.Warning(
                    "FlexRadio slice create succeeded but no slice index was confirmed: body={Body}, freqHz={FrequencyHz}, mode={Mode}, pan={PanStreamId}, ant={Ant}",
                    TruncateForLog(response.Body),
                    hz,
                    mode,
                    panStreamId ?? "(none)",
                    ant ?? "(none)");
                return null;
            }
        }

        // Create status can omit RF or reuse a slice index with a stale frequency — force the commanded tune.
        UpdateSliceFrequencyUnlocked(created.Value, hz);
        var tuneOk = SendAndWaitUnlocked(seq =>
            FlexSmartSdrCodec.BuildSliceTuneCommand(seq, created.Value, mhz));
        if (tuneOk)
            UpdateSliceFrequencyUnlocked(created.Value, hz);
        else
        {
            Log.Warning(
                "FlexRadio slice {SliceIndex} created on pan {PanStreamId} but tune to {FrequencyHz} Hz was not acknowledged",
                created.Value,
                panStreamId ?? "(none)",
                hz);
        }

        DrainPendingStatusUnlocked();
        return created;
    }

    private void SeedCreatedSliceUnlocked(int sliceIndex, long hz, string mode, string? panStreamId)
    {
        if (_slices.TryGetValue(sliceIndex, out var existing))
        {
            _slices[sliceIndex] = existing with
            {
                InUse = true,
                FrequencyHz = hz > 0 ? hz : existing.FrequencyHz,
                Mode = string.IsNullOrWhiteSpace(mode) ? existing.Mode : mode,
                PanStreamId = string.IsNullOrWhiteSpace(panStreamId)
                    ? existing.PanStreamId
                    : panStreamId
            };
        }
        else
        {
            _slices[sliceIndex] = new FlexSliceState(
                sliceIndex,
                InUse: true,
                FrequencyHz: hz,
                Mode: mode ?? "",
                IsTransmit: false,
                IsActive: false,
                FmToneMode: "",
                FmToneHz: 0,
                PanStreamId: panStreamId ?? "");
        }

        if (hz > 0)
            _sliceFrequencyRevisions[sliceIndex] =
                _sliceFrequencyRevisions.GetValueOrDefault(sliceIndex) + 1;
    }

    private bool SetPanCenterUnlocked(string panStreamId, long centerHz, out string? failureDetail)
    {
        var mhz = FlexSmartSdrCodec.HzToMhz(centerHz);
        var ok = TrySendSliceCommandUnlocked(
            seq => FlexSmartSdrCodec.BuildDisplayPanCenterCommand(seq, panStreamId, mhz),
            out failureDetail);
        if (!ok)
            return false;

        // Never invent a centre the radio did not take (silent cross-SCU rejects return R|0|).
        DrainPendingStatusUnlocked();
        if (!_pans.TryGetValue(panStreamId, out var live) || live.CenterHz <= 0)
        {
            failureDetail =
                $"no live pan centre after display pan set {panStreamId} center={mhz.ToString("0.######", CultureInfo.InvariantCulture)}";
            return false;
        }

        if (!PanCenterMatchesFrequencyBand(live.CenterHz, centerHz))
        {
            failureDetail =
                $"radio did not move pan {panStreamId} onto commanded band; live={live.CenterHz} Hz, commanded={centerHz} Hz";
            Log.Warning(
                "FlexRadio pan centre command succeeded but live centre stayed off-band: pan={PanStreamId}, liveHz={LiveHz}, commandedHz={CommandedHz}",
                panStreamId,
                live.CenterHz,
                centerHz);
            return false;
        }

        failureDetail = null;
        return true;
    }

    /// <summary>
    /// Clears sticky VHF/UHF locks whose live centres no longer sit on that band.
    /// </summary>
    private void InvalidateStaleBandPanLocksUnlocked()
    {
        if (!string.IsNullOrWhiteSpace(_lockedVhfPanStreamId))
        {
            if (!_pans.TryGetValue(_lockedVhfPanStreamId, out var vhfPan)
                || vhfPan.CenterHz <= 0
                || !RigSatModeHelper.IsVhfCenterKHz(vhfPan.CenterHz / 1000.0))
            {
                Log.Warning(
                    "FlexRadio clearing stale VHF pan lock {PanStreamId}; liveCentreHz={CenterHz}",
                    _lockedVhfPanStreamId,
                    vhfPan?.CenterHz ?? 0);
                _lockedVhfPanStreamId = null;
            }
        }

        if (!string.IsNullOrWhiteSpace(_lockedUhfPanStreamId))
        {
            if (!_pans.TryGetValue(_lockedUhfPanStreamId, out var uhfPan)
                || uhfPan.CenterHz <= 0
                || !RigSatModeHelper.IsUhfCenterKHz(uhfPan.CenterHz / 1000.0))
            {
                Log.Warning(
                    "FlexRadio clearing stale UHF pan lock {PanStreamId}; liveCentreHz={CenterHz}",
                    _lockedUhfPanStreamId,
                    uhfPan?.CenterHz ?? 0);
                _lockedUhfPanStreamId = null;
            }
        }
    }

    private bool TryLockLiveDualBandPansUnlocked()
    {
        InvalidateStaleBandPanLocksUnlocked();
        if (!FlexPanBandResolver.TryResolveBandPans(_pans.Values, out var vhfPan, out var uhfPan)
            || string.IsNullOrWhiteSpace(vhfPan)
            || string.IsNullOrWhiteSpace(uhfPan)
            || !_pans.ContainsKey(vhfPan)
            || !_pans.ContainsKey(uhfPan))
        {
            return false;
        }

        _lockedVhfPanStreamId = vhfPan;
        _lockedUhfPanStreamId = uhfPan;
        return true;
    }

    private void EnsureLockedBandPansUnlocked()
    {
        InvalidateStaleBandPanLocksUnlocked();

        if (!string.IsNullOrWhiteSpace(_lockedVhfPanStreamId)
            && !string.IsNullOrWhiteSpace(_lockedUhfPanStreamId)
            && _pans.ContainsKey(_lockedVhfPanStreamId)
            && _pans.ContainsKey(_lockedUhfPanStreamId))
        {
            return;
        }

        if (!FlexPanBandResolver.TryResolveBandPans(_pans.Values, out var vhfPan, out var uhfPan))
            return;

        if (!string.IsNullOrWhiteSpace(vhfPan) && _pans.ContainsKey(vhfPan))
            _lockedVhfPanStreamId = vhfPan;
        if (!string.IsNullOrWhiteSpace(uhfPan) && _pans.ContainsKey(uhfPan))
            _lockedUhfPanStreamId = uhfPan;
    }

    private void RemoveAllInUseSlicesUnlocked()
    {
        foreach (var index in _slices.Values.Where(s => s.InUse).Select(s => s.Index).OrderByDescending(i => i).ToList())
        {
            if (!RemoveSliceUnlocked(index, out var detail))
            {
                Log.Warning(
                    "FlexRadio failed to remove slice {SliceIndex} during dual-band pan recovery: detail={Detail}",
                    index,
                    detail);
            }
        }

        DrainPendingStatusUnlocked();
    }

    private void TryRecoverMissingBandPanUnlocked(long targetHz, bool isVhf, string? keepPanStreamId)
    {
        var candidate = FindPanCandidateForBandMoveUnlocked(isVhf, keepPanStreamId);
        if (!string.IsNullOrWhiteSpace(candidate)
            && SetPanCenterUnlocked(candidate, targetHz, out _))
        {
            DrainPendingStatusUnlocked();
            if (PanMatchesBandUnlocked(candidate, isVhf))
                return;
        }

        if (TryCreatePanafallOnBandUnlocked(targetHz, isVhf, out _))
            return;

        // Last resort: free a slice-less duplicate same-band pan, then create again.
        if (TryRemoveSliceLessDuplicatePanUnlocked(removeFromVhfBand: !isVhf, keepPanStreamId)
            && TryCreatePanafallOnBandUnlocked(targetHz, isVhf, out _))
        {
            return;
        }

        Log.Warning(
            "FlexRadio could not restore {Band} pan at {CenterHz} Hz",
            isVhf ? "VHF" : "UHF",
            targetHz);
    }

    private string? FindPanCandidateForBandMoveUnlocked(bool needVhf, string? keepPanStreamId)
    {
        // Prefer pans with no/unknown centre, then pans on the opposite (duplicate) band.
        var ordered = _pans.Values
            .Where(p => !string.IsNullOrWhiteSpace(p.StreamId))
            .Where(p => string.IsNullOrWhiteSpace(keepPanStreamId)
                        || !string.Equals(p.StreamId, keepPanStreamId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p =>
            {
                if (p.CenterHz <= 0)
                    return 0;
                var onNeededBand = needVhf
                    ? RigSatModeHelper.IsVhfCenterKHz(p.CenterHz / 1000.0)
                    : RigSatModeHelper.IsUhfCenterKHz(p.CenterHz / 1000.0);
                return onNeededBand ? 2 : 1;
            })
            .ThenBy(p => p.StreamId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return ordered.FirstOrDefault()?.StreamId;
    }

    private bool PanMatchesBandUnlocked(string panStreamId, bool isVhf)
    {
        if (!_pans.TryGetValue(panStreamId, out var pan) || pan.CenterHz <= 0)
            return false;

        return isVhf
            ? RigSatModeHelper.IsVhfCenterKHz(pan.CenterHz / 1000.0)
            : RigSatModeHelper.IsUhfCenterKHz(pan.CenterHz / 1000.0);
    }

    private bool TryCreatePanafallOnBandUnlocked(long targetHz, bool isVhf, out string? panStreamId)
    {
        panStreamId = null;
        var response = SendAndWaitResponseUnlocked(FlexSmartSdrCodec.BuildDisplayPanafallCreateCommand);
        if (response is null || !FlexSmartSdrCodec.IsSuccessResponse(response)
            || !FlexSmartSdrCodec.TryParsePanafallCreatePanId(response.Body, out var createdPan))
        {
            response = SendAndWaitResponseUnlocked(FlexSmartSdrCodec.BuildPanadapterCreateCommand);
            if (response is null || !FlexSmartSdrCodec.IsSuccessResponse(response)
                || !FlexSmartSdrCodec.TryParsePanafallCreatePanId(response.Body, out createdPan))
            {
                Log.Warning(
                    "FlexRadio panafall/panadapter create failed while restoring {Band} pan",
                    isVhf ? "VHF" : "UHF");
                return false;
            }
        }

        DrainPendingStatusUnlocked();
        if (!_pans.ContainsKey(createdPan))
            UpdatePanCenterUnlocked(createdPan, 0, autoCenter: false);

        if (!SetPanCenterUnlocked(createdPan, targetHz, out var centreFailure))
        {
            // Creating a pan often lands near HF; if centre failed, try once more after drain.
            DrainPendingStatusUnlocked();
            if (!SetPanCenterUnlocked(createdPan, targetHz, out centreFailure))
            {
                Log.Warning(
                    "FlexRadio created pan {PanStreamId} but could not centre it on {Band}: detail={Detail}",
                    createdPan,
                    isVhf ? "VHF" : "UHF",
                    centreFailure);
                return false;
            }
        }

        panStreamId = createdPan;
        Log.Information(
            "FlexRadio created {Band} pan {PanStreamId} at {CenterHz} Hz",
            isVhf ? "VHF" : "UHF",
            createdPan,
            targetHz);
        return true;
    }

    private bool TryRemoveSliceLessDuplicatePanUnlocked(bool removeFromVhfBand, string? keepPanStreamId)
    {
        var candidates = _pans.Values
            .Where(p => !string.IsNullOrWhiteSpace(p.StreamId) && p.CenterHz > 0)
            .Where(p => string.IsNullOrWhiteSpace(keepPanStreamId)
                        || !string.Equals(p.StreamId, keepPanStreamId, StringComparison.OrdinalIgnoreCase))
            .Where(p => removeFromVhfBand
                ? RigSatModeHelper.IsVhfCenterKHz(p.CenterHz / 1000.0)
                : RigSatModeHelper.IsUhfCenterKHz(p.CenterHz / 1000.0))
            .Where(p => !_slices.Values.Any(s =>
                s.InUse
                && !string.IsNullOrWhiteSpace(s.PanStreamId)
                && string.Equals(s.PanStreamId, p.StreamId, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(p => p.StreamId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var victim = candidates.FirstOrDefault();
        if (victim is null)
            return false;

        var removed = TrySendSliceCommandUnlocked(
            seq => FlexSmartSdrCodec.BuildDisplayPanRemoveCommand(seq, victim.StreamId),
            out _);
        _ = TrySendSliceCommandUnlocked(
            seq => FlexSmartSdrCodec.BuildDisplayPanafallRemoveCommand(seq, victim.StreamId),
            out _);
        if (removed)
            _pans.Remove(victim.StreamId);

        DrainPendingStatusUnlocked();
        Log.Warning(
            "FlexRadio removed slice-less duplicate pan {PanStreamId} to free capacity for dual-band recovery",
            victim.StreamId);
        return removed;
    }

    private string? ResolveLockedPanForFrequencyUnlocked(long hz)
    {
        if (hz <= 0)
            return null;

        if (RigSatModeHelper.IsVhfCenterKHz(hz / 1000.0))
            return _lockedVhfPanStreamId;
        if (RigSatModeHelper.IsUhfCenterKHz(hz / 1000.0))
            return _lockedUhfPanStreamId;

        return null;
    }

    private void ClearLockedBandPansUnlocked()
    {
        _lockedVhfPanStreamId = null;
        _lockedUhfPanStreamId = null;
    }

    private bool TrySendSliceCommandUnlocked(Func<uint, string> commandFactory, out string? failureDetail)
    {
        var response = SendAndWaitResponseUnlocked(commandFactory);
        if (response is null)
        {
            failureDetail = "timeout waiting for SmartSDR response";
            return false;
        }

        if (FlexSmartSdrCodec.IsSuccessResponse(response))
        {
            failureDetail = null;
            return true;
        }

        failureDetail =
            $"hex=0x{response.HexResponse:X8}, body={TruncateForLog(response.Body)}";
        return false;
    }

    private static string TruncateForLog(string? body)
    {
        if (string.IsNullOrEmpty(body))
            return "";
        return body.Length <= 120 ? body : body[..120] + "…";
    }

    public bool SetSliceTone(int sliceIndex, bool toneOn, double toneHz)
    {
        lock (_gate)
        {
            EnsureConnectedUnlocked();
            var modeSet = SendAndWaitUnlocked(seq =>
                FlexSmartSdrCodec.BuildSliceSetToneModeCommand(seq, sliceIndex, toneOn));
            if (!modeSet)
                return false;

            if (_slices.TryGetValue(sliceIndex, out var modeSlice))
                _slices[sliceIndex] = modeSlice with { FmToneMode = toneOn ? "ctcss_tx" : "OFF" };

            if (!toneOn)
                return true;

            var valueSet = SendAndWaitUnlocked(seq =>
                FlexSmartSdrCodec.BuildSliceSetToneValueCommand(seq, sliceIndex, toneHz));
            if (valueSet && _slices.TryGetValue(sliceIndex, out var valueSlice))
                _slices[sliceIndex] = valueSlice with { FmToneHz = toneHz };

            return valueSet;
        }
    }

    public int? CreateSlice(long hz, string mode, string? ant = null, string? panStreamId = null)
    {
        lock (_gate)
        {
            EnsureConnectedUnlocked();
            return CreateSliceUnlocked(hz, mode, ant, panStreamId);
        }
    }

    public long? GetSliceFrequencyHz(int sliceIndex)
    {
        lock (_gate)
        {
            DrainPendingStatusUnlocked();
            return _slices.TryGetValue(sliceIndex, out var s) && s.FrequencyHz > 0
                ? s.FrequencyHz
                : null;
        }
    }

    public FlexSliceFrequencyObservation? GetSliceFrequencyObservation(int sliceIndex)
    {
        lock (_gate)
        {
            DrainPendingStatusUnlocked();
            return _slices.TryGetValue(sliceIndex, out var slice) && slice.FrequencyHz > 0
                ? new FlexSliceFrequencyObservation(
                    slice.FrequencyHz,
                    _sliceFrequencyRevisions.GetValueOrDefault(sliceIndex))
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

    private void RequireCommandUnlocked(Func<uint, string> commandFactory, string operation)
    {
        if (!SendAndWaitUnlocked(commandFactory))
            throw new InvalidOperationException($"Flex SmartSDR failed to {operation}.");
    }

    private int? WaitForCreatedSliceUnlocked(HashSet<int> existingIndexes, int? responseIndex)
    {
        int? Resolve()
        {
            if (responseIndex is { } confirmed
                && !existingIndexes.Contains(confirmed)
                && _slices.TryGetValue(confirmed, out var responseSlice)
                && responseSlice.InUse)
            {
                return confirmed;
            }

            var newIndexes = _slices.Values
                .Where(slice => slice.InUse && !existingIndexes.Contains(slice.Index))
                .Select(slice => slice.Index)
                .Distinct()
                .Take(2)
                .ToList();
            return newIndexes.Count == 1 ? newIndexes[0] : null;
        }

        var resolved = Resolve();
        if (resolved is not null)
            return resolved;

        var deadline = DateTime.UtcNow.AddMilliseconds(_commandTimeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (!TryReadLineUnlocked(deadline, out var line) || string.IsNullOrEmpty(line))
                continue;

            ProcessIncomingLineUnlocked(line);
            resolved = Resolve();
            if (resolved is not null)
                return resolved;
        }

        return null;
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
            var hasFrequency = HasSliceField(body, "RF_frequency") || HasSliceField(body, "freq");
            if (_slices.TryGetValue(slice.Index, out var existing))
            {
                slice = existing with
                {
                    InUse = HasSliceField(body, "in_use") ? slice.InUse : existing.InUse,
                    FrequencyHz = hasFrequency
                        ? slice.FrequencyHz
                        : existing.FrequencyHz,
                    Mode = HasSliceField(body, "mode") ? slice.Mode : existing.Mode,
                    IsTransmit = HasSliceField(body, "tx") ? slice.IsTransmit : existing.IsTransmit,
                    IsActive = HasSliceField(body, "active") ? slice.IsActive : existing.IsActive,
                    FmToneMode = HasSliceField(body, "fm_tone_mode")
                        ? slice.FmToneMode
                        : existing.FmToneMode,
                    FmToneHz = HasSliceField(body, "fm_tone_value")
                        ? slice.FmToneHz
                        : existing.FmToneHz,
                    PanStreamId = HasSliceField(body, "pan")
                        ? slice.PanStreamId
                        : existing.PanStreamId
                };
            }
            else if (!HasSliceField(body, "in_use"))
            {
                // Brand-new cache entry without in_use must not become a ghost duplex slice.
                slice = slice with { InUse = false };
            }

            _slices[slice.Index] = slice;
            if (hasFrequency)
                _sliceFrequencyRevisions[slice.Index] =
                    _sliceFrequencyRevisions.GetValueOrDefault(slice.Index) + 1;
            return;
        }

        if (FlexSmartSdrCodec.TryParseRadioFullDuplex(body, out var fdx))
            _fullDuplexEnabled = fdx;
        else if (FlexSmartSdrCodec.TryParseDisplayPanStatus(body, out var pan))
            ApplyPanStatusUnlocked(pan, body);
    }

    private void ApplyPanStatusUnlocked(FlexPanState pan, string body)
    {
        if (_pans.TryGetValue(pan.StreamId, out var existing))
        {
            pan = existing with
            {
                CenterHz = HasPanField(body, "center") ? pan.CenterHz : existing.CenterHz,
                AutoCenter = HasPanField(body, "autocenter") ? pan.AutoCenter : existing.AutoCenter
            };
        }

        _pans[pan.StreamId] = pan;
    }

    private void UpdatePanCenterUnlocked(string panStreamId, long centerHz, bool autoCenter)
    {
        if (_pans.TryGetValue(panStreamId, out var existing))
            _pans[panStreamId] = existing with { CenterHz = centerHz, AutoCenter = autoCenter };
        else
            _pans[panStreamId] = new FlexPanState(panStreamId, centerHz, autoCenter);
    }

    private static bool HasPanField(string statusBody, string field) =>
        statusBody.Contains($" {field}=", StringComparison.OrdinalIgnoreCase);

    private static bool HasSliceField(string statusBody, string field) =>
        statusBody.Contains($" {field}=", StringComparison.OrdinalIgnoreCase);

    private void UpdateSliceFrequencyUnlocked(int sliceIndex, long hz)
    {
        if (_slices.TryGetValue(sliceIndex, out var existing))
            _slices[sliceIndex] = existing with { FrequencyHz = hz, InUse = true };
        else
            _slices[sliceIndex] = new FlexSliceState(
                sliceIndex, true, hz, "", false, false, "", 0);
    }

    private void DrainPendingStatusUnlocked()
    {
        while (ExtractLineFromBuffer() is { } buffered)
            ProcessIncomingLineUnlocked(buffered);

        if (_stream is null)
            return;

        try
        {
            while (_stream.DataAvailable)
            {
                var read = _stream.Read(_readBuffer, 0, _readBuffer.Length);
                if (read <= 0)
                    break;

                _lineBuffer.Append(Encoding.ASCII.GetString(_readBuffer, 0, read));
                while (ExtractLineFromBuffer() is { } line)
                    ProcessIncomingLineUnlocked(line);
            }
        }
        catch (IOException ex)
        {
            Log.Debug(ex, "Flex SmartSDR non-blocking status drain failed");
        }
        catch (ObjectDisposedException)
        {
        }
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
        _handle = "";
        _version = "";
        _fullDuplexEnabled = false;
        _slices.Clear();
        _pans.Clear();
        ClearLockedBandPansUnlocked();
        _sliceFrequencyRevisions.Clear();
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

internal readonly record struct FlexSliceFrequencyObservation(long FrequencyHz, long StatusRevision);
