using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using Serilog;

namespace OscarWatch.Rig;

/// <summary>
/// FlexRadio SmartSDR driver for full-duplex satellite tracking (Main = RX slice, Sub = TX slice).
/// </summary>
public sealed class FlexRadioDriver : IRigDriver
{
    private static readonly ILogger Log = Serilog.Log.ForContext<FlexRadioDriver>();

    private readonly FlexSmartSdrClient _client;
    private readonly bool _ownsClient;
    private RigVfo _currentVfo = RigVfo.Main;
    private int _rxSliceIndex = 0;
    private int _txSliceIndex = 1;
    private bool _satelliteMode;
    private long _lastMainHz;
    private long _lastSubHz;
    private long _lastMainStatusRevision;
    private long _lastMainObservedHz;
    private bool _hasMainStatusObservation;
    private bool _toneOn;
    private bool _toneApplied;
    private double _toneHz = 67.0;
    private RigSettings _antennaPortSettings = new();

    public FlexRadioDriver(string host, int port, int catDelayMs = 50)
        : this(new FlexSmartSdrClient(host, port, ResolveTimeoutMs(catDelayMs)), ownsClient: true)
    {
    }

    internal FlexRadioDriver(FlexSmartSdrClient client, bool ownsClient = false)
    {
        _client = client;
        _ownsClient = ownsClient;
    }

    public RigType RigType => RigType.FlexSmartSdr;
    public bool IsConnected => _client.IsConnected;
    public bool SupportsTracking => true;
    public bool SupportsVfoExchange => false;
    public bool IsSatelliteModeActive => _satelliteMode;

    /// <summary>RX (downlink) slice index after satellite setup.</summary>
    public int RxSliceIndex => _rxSliceIndex;

    /// <summary>TX (uplink) slice index after satellite setup.</summary>
    public int TxSliceIndex => _txSliceIndex;

    public void Open() => _client.Open();

    public long? ReadFrequencyHz(RigVfo vfo)
    {
        var slice = SliceFor(vfo);
        var cached = CachedFrequencyHz(vfo);
        if (!_client.IsConnected)
            return cached > 0 ? cached : null;

        long? hz;
        if (vfo is RigVfo.Main or RigVfo.VfoA)
        {
            var observation = _client.GetSliceFrequencyObservation(slice);
            hz = observation?.FrequencyHz;
            if (observation is { } observed)
            {
                _lastMainStatusRevision = observed.StatusRevision;
                _lastMainObservedHz = observed.FrequencyHz;
                _hasMainStatusObservation = true;
            }
        }
        else
        {
            hz = _client.GetSliceFrequencyHz(slice);
        }

        if (hz is > 0)
        {
            StoreFrequencyHz(vfo, hz.Value);
            return hz;
        }

        return cached > 0 ? cached : null;
    }

    public bool SetFrequencyHz(long hz)
    {
        if (hz <= 0)
            return false;

        if (!_client.IsConnected)
        {
            StoreFrequencyHz(_currentVfo, hz);
            return true;
        }

        var isReceive = _currentVfo is RigVfo.Main or RigVfo.VfoA;
        var tuned = isReceive && _hasMainStatusObservation
            ? _client.TuneSliceIfStatusUnchanged(
                SliceFor(_currentVfo),
                hz,
                _lastMainStatusRevision,
                _lastMainObservedHz)
            : _client.TuneSlice(SliceFor(_currentVfo), hz);
        if (tuned)
            StoreFrequencyHz(_currentVfo, hz);
        return tuned;
    }

    public void SelectVfo(RigVfo vfo, bool force = false) => _currentVfo = vfo;

    public void SetMode(string mode)
    {
        var smart = FlexModeMapper.ToSmartSdrMode(mode);
        if (smart is null || !_client.IsConnected)
            return;

        var sliceIndex = SliceFor(_currentVfo);
        if (!_client.SetSliceMode(sliceIndex, smart))
        {
            Log.Warning(
                "FlexRadio failed to set mode on slice {SliceIndex}: mode={Mode}",
                sliceIndex,
                smart);
        }
    }

    public void SetSplitOn(bool on)
    {
        // Full duplex uses two slices; classic split is not used.
    }

    public void SetSatelliteMode(bool on)
    {
        if (!_client.IsConnected)
            return;

        if (!on)
        {
            var disabled = _client.SetFullDuplex(false);
            _satelliteMode = false;
            Log.Information("FlexRadio satellite mode disabled; full duplex command succeeded={Succeeded}", disabled);
            return;
        }

        if (!_client.SetFullDuplex(true))
        {
            _satelliteMode = false;
            throw new FlexSatelliteSetupException("SmartSDR did not enable full duplex.");
        }

        if (!EnsureDuplexSlicesWithRetry())
        {
            DisableFullDuplexAfterSetupFailure();
            throw new FlexSatelliteSetupException("SmartSDR could not establish separate RX and TX slices.");
        }

        if (!_client.SetSliceTx(_txSliceIndex, true))
        {
            DisableFullDuplexAfterSetupFailure();
            throw new FlexSatelliteSetupException($"SmartSDR did not mark slice {_txSliceIndex} as the TX slice.");
        }

        _satelliteMode = true;
        Log.Information(
            "FlexRadio satellite mode enabled; full duplex=true, RX slice={RxSliceIndex}, TX slice={TxSliceIndex}",
            _rxSliceIndex,
            _txSliceIndex);
    }

    /// <summary>
    /// Stores band→port settings used when creating slices and applying antenna ports.
    /// </summary>
    public void ConfigureAntennaPorts(RigSettings settings) =>
        _antennaPortSettings = settings;

    /// <summary>
    /// Applies configured VHF/UHF RX and TX antenna ports for the current duplex slices.
    /// Receive ports are applied by band to each slice's <c>rxant</c>; transmit ports apply to the uplink slice's <c>txant</c>.
    /// Empty settings leave SmartSDR ports unchanged.
    /// </summary>
    public void ApplyBandAntennaPorts(RigSettings settings, long downlinkHz, long uplinkHz)
    {
        _antennaPortSettings = settings;
        if (!_client.IsConnected)
            return;

        var downlinkRxAnt = FlexAntennaPortResolver.ResolveRxPort(settings, downlinkHz);
        if (downlinkRxAnt is not null)
        {
            if (!_client.SetSliceRxAnt(_rxSliceIndex, downlinkRxAnt, out var rxFailure))
            {
                Log.Warning(
                    "FlexRadio failed to set RX antenna on slice {SliceIndex}: port={Port}, downlinkHz={DownlinkHz}, detail={Detail}",
                    _rxSliceIndex,
                    downlinkRxAnt,
                    downlinkHz,
                    rxFailure);
            }
        }

        if (!_satelliteMode || uplinkHz <= 0)
            return;

        var uplinkRxAnt = FlexAntennaPortResolver.ResolveRxPort(settings, uplinkHz);
        if (uplinkRxAnt is not null)
        {
            if (!_client.SetSliceRxAnt(_txSliceIndex, uplinkRxAnt, out var uplinkRxFailure))
            {
                Log.Warning(
                    "FlexRadio failed to set RX antenna on slice {SliceIndex}: port={Port}, uplinkHz={UplinkHz}, detail={Detail}",
                    _txSliceIndex,
                    uplinkRxAnt,
                    uplinkHz,
                    uplinkRxFailure);
            }
        }

        var txAnt = FlexAntennaPortResolver.ResolveTxPort(settings, uplinkHz);
        if (txAnt is not null && !_client.SetSliceTxAnt(_txSliceIndex, txAnt, out var txFailure))
        {
            Log.Warning(
                "FlexRadio failed to set TX antenna on slice {SliceIndex}: port={Port}, uplinkHz={UplinkHz}, detail={Detail}",
                _txSliceIndex,
                txAnt,
                uplinkHz,
                txFailure);
        }
    }

    /// <summary>
    /// Restores separate live VHF and UHF panadapters before slice bind.
    /// Recovers from both-pans-on-one-band by recentring or creating a panafall.
    /// </summary>
    public bool EnsureDualBandPanLayout(long downlinkHz, long uplinkHz, bool allowRemoveInUseSlices = true)
    {
        if (!_client.IsConnected || !_satelliteMode)
            return false;

        FlexPanBandResolver.ResolveTargetFrequencies(
            downlinkHz,
            uplinkHz,
            _satelliteMode,
            out var vhfHz,
            out var uhfHz);

        if (vhfHz <= 0 || uhfHz <= 0)
        {
            Log.Warning(
                "FlexRadio dual-band pan ensure skipped; could not resolve VHF/UHF targets from downlinkHz={DownlinkHz}, uplinkHz={UplinkHz}",
                downlinkHz,
                uplinkHz);
            return false;
        }

        var ok = _client.EnsureDualBandPanLayout(vhfHz, uhfHz, allowRemoveInUseSlices);
        if (!ok)
        {
            Log.Warning(
                "FlexRadio failed to ensure separate VHF and UHF panadapters before bind: downlinkHz={DownlinkHz}, uplinkHz={UplinkHz}",
                downlinkHz,
                uplinkHz);
        }

        return ok;
    }

    /// <summary>
    /// Binds RX/TX slices to locked VHF/UHF panadapters before initial tune on band changes.
    /// </summary>
    /// <param name="forceRebind">
    /// When true (V/U↔U/V layout flip), recreate even if cached pan IDs look plausible.
    /// </param>
    public void BindDuplexSlicesToBandPans(long downlinkHz, long uplinkHz, bool forceRebind = false)
    {
        if (!_client.IsConnected || !_satelliteMode)
            return;

        _client.ResolveDuplexSliceRoles(ref _rxSliceIndex, ref _txSliceIndex);

        const int maxAttempts = 2;
        const int retryDelayMs = 100;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (_client.BindDuplexSlicesToBandPans(
                    ref _rxSliceIndex,
                    ref _txSliceIndex,
                    downlinkHz,
                    uplinkHz,
                    _satelliteMode,
                    forceRebind))
                return;

            if (attempt < maxAttempts)
            {
                Log.Debug(
                    "FlexRadio slice-to-pan bind attempt {Attempt}/{MaxAttempts} failed; retrying",
                    attempt,
                    maxAttempts);
                Thread.Sleep(retryDelayMs);
            }
        }

        Log.Warning(
            "FlexRadio failed to bind duplex slices to band panadapters: downlinkHz={DownlinkHz}, uplinkHz={UplinkHz}, forceRebind={ForceRebind}",
            downlinkHz,
            uplinkHz,
            forceRebind);
    }

    /// <summary>
    /// One-shot: centre each band panadapter (VHF SCU / UHF SCU) after pass init.
    /// Uses pan display band, not slice pan association, so U/V after V/U still splits correctly.
    /// Continuous Doppler uses autopan=0 so these pans are not yanked again.
    /// </summary>
    public void CenterBandPanadapters(long downlinkHz, long uplinkHz)
    {
        if (!_client.IsConnected)
            return;

        FlexPanBandResolver.ResolveTargetFrequencies(
            downlinkHz,
            uplinkHz,
            _satelliteMode,
            out var vhfHz,
            out var uhfHz);

        var centred = _client.CenterBandPans(downlinkHz, uplinkHz, _satelliteMode);
        if (!centred && _satelliteMode && downlinkHz > 0 && uplinkHz > 0 && vhfHz > 0 && uhfHz > 0)
        {
            Log.Warning(
                "FlexRadio pan centre failed; restoring dual-band pans and retrying centre: downlinkHz={DownlinkHz}, uplinkHz={UplinkHz}",
                downlinkHz,
                uplinkHz);
            EnsureDualBandPanLayout(downlinkHz, uplinkHz);
            _client.TryRelockBandPansFromLiveCentres();
            centred = _client.CenterBandPans(downlinkHz, uplinkHz, _satelliteMode);
        }

        _client.GetLockedBandPanStreamIds(out var lockedVhf, out var lockedUhf);

        if (!centred)
        {
            Log.Warning(
                "FlexRadio failed to centre one or more band panadapters: downlinkHz={DownlinkHz}, uplinkHz={UplinkHz}, vhfPan={VhfPan}, uhfPan={UhfPan}",
                downlinkHz,
                uplinkHz,
                lockedVhf ?? "(none)",
                lockedUhf ?? "(none)");
        }
        else
        {
            if (vhfHz > 0)
            {
                Log.Information(
                    "FlexRadio centred VHF pan {PanStreamId} at {CenterMhz:0.###} MHz",
                    lockedVhf ?? "(unresolved)",
                    FlexSmartSdrCodec.HzToMhz(vhfHz));
            }

            if (_satelliteMode && uhfHz > 0)
            {
                Log.Information(
                    "FlexRadio centred UHF pan {PanStreamId} at {CenterMhz:0.###} MHz",
                    lockedUhf ?? "(unresolved)",
                    FlexSmartSdrCodec.HzToMhz(uhfHz));
            }
        }

        var rxPan = _client.GetSlicePanStreamId(_rxSliceIndex);
        var txPan = _client.GetSlicePanStreamId(_txSliceIndex);
        if (!string.IsNullOrWhiteSpace(rxPan)
            && !string.IsNullOrWhiteSpace(txPan)
            && string.Equals(rxPan, txPan, StringComparison.OrdinalIgnoreCase)
            && downlinkHz > 0
            && uplinkHz > 0)
        {
            Log.Warning(
                "FlexRadio RX and TX slices share pan {PanStreamId}; both bands cannot stay on screen until each slice has its own panadapter",
                rxPan);
        }
    }

    /// <summary>
    /// After bind/tune/centre/modes, confirm live RX/TX layout. Retune and re-apply modes, and if needed
    /// restore dual-band pans and force-rebind, when the radio did not land on the commanded pass.
    /// </summary>
    public void EnsureDuplexPassFrequencies(
        long downlinkHz,
        long uplinkHz,
        string? expectedRxMode = null,
        string? expectedTxMode = null)
    {
        if (!_client.IsConnected || !_satelliteMode)
            return;

        _client.ResolveDuplexSliceRoles(ref _rxSliceIndex, ref _txSliceIndex);

        if (DuplexFrequenciesVerified(downlinkHz, uplinkHz, out var mismatch, expectedRxMode, expectedTxMode))
            return;

        Log.Warning(
            "FlexRadio pass layout mismatch after init: {Detail}; retuning, recentring, and reapplying modes",
            mismatch);

        ApplyDuplexLightRepair(downlinkHz, uplinkHz, expectedRxMode, expectedTxMode);

        if (DuplexFrequenciesVerified(downlinkHz, uplinkHz, out _, expectedRxMode, expectedTxMode))
        {
            Log.Information(
                "FlexRadio pass layout repaired without rebind: RX={RxHz} Hz, TX={TxHz} Hz",
                downlinkHz,
                uplinkHz);
            // Pan recovery during light repair may have recreated slices; restore band ports.
            ApplyBandAntennaPorts(_antennaPortSettings, downlinkHz, uplinkHz);
            return;
        }

        DuplexFrequenciesVerified(downlinkHz, uplinkHz, out var afterRetune, expectedRxMode, expectedTxMode);
        Log.Warning(
            "FlexRadio pass layout still wrong after light repair: {Detail}; restoring dual-band pans and force-rebinding",
            afterRetune);

        EnsureDualBandPanLayout(downlinkHz, uplinkHz);
        _client.TryRelockBandPansFromLiveCentres();
        BindDuplexSlicesToBandPans(downlinkHz, uplinkHz, forceRebind: true);
        ApplyDuplexLightRepair(downlinkHz, uplinkHz, expectedRxMode, expectedTxMode);

        if (DuplexFrequenciesVerified(downlinkHz, uplinkHz, out var stillWrong, expectedRxMode, expectedTxMode))
        {
            Log.Information(
                "FlexRadio pass layout repaired by dual-band recovery and force rebind: RX={RxHz} Hz, TX={TxHz} Hz",
                downlinkHz,
                uplinkHz);
        }
        else
        {
            Log.Warning(
                "FlexRadio pass layout still incorrect after dual-band recovery: {Detail}. If the radio is at pan/SCU capacity or another client owns the pans, load a SmartSDR Global Profile that restores separate VHF and UHF pans, then re-select the satellite in OscarWatch.",
                stillWrong);
        }

        // Slice recreate during force rebind drops rxant/txant; re-apply band ports.
        ApplyBandAntennaPorts(_antennaPortSettings, downlinkHz, uplinkHz);
    }

    private bool DuplexFrequenciesVerified(
        long downlinkHz,
        long uplinkHz,
        out string detail,
        string? expectedRxMode = null,
        string? expectedTxMode = null) =>
        _client.VerifyDuplexSliceFrequencies(
            _rxSliceIndex,
            _txSliceIndex,
            downlinkHz,
            uplinkHz,
            _satelliteMode,
            out detail,
            expectedRxMode,
            expectedTxMode);

    private void ApplyDuplexLightRepair(
        long downlinkHz,
        long uplinkHz,
        string? expectedRxMode,
        string? expectedTxMode)
    {
        // Centre pans onto the target bands first, then tune slices, then centre again so a stale
        // pan centre cannot leave both displays on one band after a layout flip.
        CenterBandPanadapters(downlinkHz, uplinkHz);
        RetuneDuplexSlices(downlinkHz, uplinkHz);
        CenterBandPanadapters(downlinkHz, uplinkHz);

        if (!string.IsNullOrWhiteSpace(expectedRxMode))
            _client.SetSliceMode(_rxSliceIndex, expectedRxMode);
        if (!string.IsNullOrWhiteSpace(expectedTxMode))
            _client.SetSliceMode(_txSliceIndex, expectedTxMode);

        if (_satelliteMode && uplinkHz > 0)
            _client.SetSliceTx(_txSliceIndex, tx: true);
    }

    private void RetuneDuplexSlices(long downlinkHz, long uplinkHz)
    {
        if (downlinkHz > 0)
            _client.TuneSlice(_rxSliceIndex, downlinkHz);
        if (_satelliteMode && uplinkHz > 0)
            _client.TuneSlice(_txSliceIndex, uplinkHz);
    }

    public void ExchangeVfos()
    {
        // Slice roles stay RX/TX; front-panel / SmartSDR swap is not mirrored.
        (_rxSliceIndex, _txSliceIndex) = (_txSliceIndex, _rxSliceIndex);
        (_lastMainHz, _lastSubHz) = (_lastSubHz, _lastMainHz);
        _hasMainStatusObservation = false;
    }

    public void SetToneOn(bool on)
    {
        _toneOn = on;
        ApplyTone();
    }

    public void SetToneSquelchOn(bool on)
    {
        // SmartSDR FM encode tone; squelch tone is not separately modelled in v1.
        _toneOn = on;
        ApplyTone();
    }

    public void SetToneHz(double hz, bool squelchTone)
    {
        if (hz > 0)
            _toneHz = hz;

        _toneOn = true;
        ApplyTone();
    }

    public void Dispose()
    {
        if (_ownsClient)
            _client.Dispose();
    }

    private void ApplyTone()
    {
        if (!_client.IsConnected)
            return;

        if (!_toneOn)
        {
            if (!_toneApplied)
                return;

            _toneApplied = false;
        }

        if (!_client.SetSliceTone(_txSliceIndex, _toneOn, _toneHz))
        {
            Log.Warning(
                "FlexRadio failed to confirm CTCSS on TX slice {SliceIndex}: enabled={Enabled}, toneHz={ToneHz}",
                _txSliceIndex,
                _toneOn,
                _toneHz);
            return;
        }

        if (_toneOn)
            _toneApplied = true;
    }

    private void DisableFullDuplexAfterSetupFailure()
    {
        _satelliteMode = false;
        try
        {
            _client.SetFullDuplex(false);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "FlexRadio full duplex cleanup after setup failure failed");
        }
    }

    /// <summary>
    /// After reconnect SmartSDR sometimes reports zero slices briefly; create/status can lag.
    /// Retry a few times before failing pass init (matches field recovery on immediate re-init).
    /// </summary>
    private bool EnsureDuplexSlicesWithRetry()
    {
        const int maxAttempts = 5;
        const int retryDelayMs = 100;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (TryEnsureDuplexSlices())
                return true;

            if (attempt < maxAttempts)
            {
                Log.Debug(
                    "FlexRadio duplex slice setup attempt {Attempt}/{MaxAttempts} failed; retrying",
                    attempt,
                    maxAttempts);
                Thread.Sleep(retryDelayMs);
            }
        }

        return false;
    }

    private bool TryEnsureDuplexSlices()
    {
        // Frequency-less in-use flags are treated as ghosts (partial status without RF).
        var slices = _client.GetInUseSlices()
            .Where(s => s.FrequencyHz > 0)
            .ToList();
        var tx = slices.FirstOrDefault(s => s.IsTransmit);

        if (slices.Count >= 2)
        {
            if (tx is not null)
            {
                _txSliceIndex = tx.Index;
                _rxSliceIndex = slices.FirstOrDefault(s => s.Index != tx.Index)?.Index ?? (tx.Index == 0 ? 1 : 0);
            }
            else
            {
                _rxSliceIndex = slices[0].Index;
                _txSliceIndex = slices[1].Index;
            }

            return DuplexSlicePairIsUsable(_rxSliceIndex, _txSliceIndex);
        }

        if (slices.Count == 1)
        {
            _rxSliceIndex = slices[0].Index;
            var existingHz = slices[0].FrequencyHz;
            ResolveSinglePanBootstrapTargets(
                existingHz,
                out var createHz,
                out var vhfHz,
                out var uhfHz);

            // Build a real VHF+UHF pan pair before the peer slice, instead of letting
            // slice create invent a second pan that later fails cross-band centre/rebind.
            // Do not strip the only live slice if recovery needs a second pass.
            string? targetPan = null;
            if (_client.EnsureDualBandPanLayout(vhfHz, uhfHz, allowRemoveInUseSlices: false))
            {
                _client.GetLockedBandPanStreamIds(out var vhfPan, out var uhfPan);
                targetPan = RigSatModeHelper.IsVhfCenterKHz(createHz / 1000.0) ? vhfPan : uhfPan;
            }

            // Create without ant=; ApplyBandAntennaPorts sets rxant/txant after bind.
            var created = _client.CreateSlice(createHz, "USB", panStreamId: targetPan);
            if (created is null || created.Value == _rxSliceIndex)
                return false;

            _txSliceIndex = created.Value;
            return DuplexSlicePairIsUsable(_rxSliceIndex, _txSliceIndex);
        }

        var rxCreateHz = _lastMainHz > 0 ? _lastMainHz : 145_900_000;
        var txCreateHz = _lastSubHz > 0 ? _lastSubHz : 435_000_000;
        var rxCreated = _client.CreateSlice(rxCreateHz, "USB");
        var txCreated = _client.CreateSlice(txCreateHz, "USB");
        if (rxCreated is null || txCreated is null || rxCreated.Value == txCreated.Value)
            return false;

        _rxSliceIndex = rxCreated.Value;
        _txSliceIndex = txCreated.Value;
        return DuplexSlicePairIsUsable(_rxSliceIndex, _txSliceIndex);
    }

    private bool DuplexSlicePairIsUsable(int rxSliceIndex, int txSliceIndex)
    {
        if (rxSliceIndex == txSliceIndex)
            return false;

        var slices = _client.GetInUseSlices();
        return slices.Any(s => s.Index == rxSliceIndex && s.FrequencyHz > 0)
            && slices.Any(s => s.Index == txSliceIndex && s.FrequencyHz > 0);
    }

    /// <summary>
    /// Chooses an opposite-band peer frequency when starting from a single pan/slice.
    /// </summary>
    private void ResolveSinglePanBootstrapTargets(
        long existingHz,
        out long createHz,
        out long vhfHz,
        out long uhfHz)
    {
        if (RigSatModeHelper.IsUhfCenterKHz(existingHz / 1000.0))
        {
            createHz = _lastMainHz > 0 && RigSatModeHelper.IsVhfCenterKHz(_lastMainHz / 1000.0)
                ? _lastMainHz
                : 145_900_000;
            uhfHz = existingHz;
            vhfHz = createHz;
            return;
        }

        createHz = _lastSubHz > 0 && RigSatModeHelper.IsUhfCenterKHz(_lastSubHz / 1000.0)
            ? _lastSubHz
            : 435_000_000;
        vhfHz = existingHz > 0 && RigSatModeHelper.IsVhfCenterKHz(existingHz / 1000.0)
            ? existingHz
            : 145_900_000;
        uhfHz = createHz;
    }

    private int SliceFor(RigVfo vfo) =>
        vfo is RigVfo.Sub or RigVfo.VfoB ? _txSliceIndex : _rxSliceIndex;

    private long CachedFrequencyHz(RigVfo vfo) =>
        vfo is RigVfo.Sub or RigVfo.VfoB ? _lastSubHz : _lastMainHz;

    private void StoreFrequencyHz(RigVfo vfo, long hz)
    {
        if (vfo is RigVfo.Sub or RigVfo.VfoB)
            _lastSubHz = hz;
        else
            _lastMainHz = hz;
    }

    /// <summary>
    /// SmartSDR command wait budget. Floor matches <see cref="FlexSmartSdrClient"/>'s 2s default so
    /// Open's multi-subscribe handshake is not starved when CatDelayMs is the Doppler default (50).
    /// </summary>
    private static int ResolveTimeoutMs(int catDelayMs) =>
        Math.Max(2000, Math.Max(0, catDelayMs) * 20);
}

public sealed class FlexSatelliteSetupException : InvalidOperationException
{
    public FlexSatelliteSetupException(string message)
        : base(message)
    {
    }
}
