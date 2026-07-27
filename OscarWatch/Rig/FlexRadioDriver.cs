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

        _client.SetSliceMode(SliceFor(_currentVfo), smart);
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
    /// Empty settings leave SmartSDR ports unchanged.
    /// </summary>
    public void ApplyBandAntennaPorts(RigSettings settings, long downlinkHz, long uplinkHz)
    {
        _antennaPortSettings = settings;
        if (!_client.IsConnected)
            return;

        var rxAnt = FlexAntennaPortResolver.ResolveRxPort(settings, downlinkHz);
        if (rxAnt is not null)
        {
            if (!_client.SetSliceRxAnt(_rxSliceIndex, rxAnt, out var rxFailure))
            {
                Log.Warning(
                    "FlexRadio failed to set RX antenna on slice {SliceIndex}: port={Port}, downlinkHz={DownlinkHz}, detail={Detail}",
                    _rxSliceIndex,
                    rxAnt,
                    downlinkHz,
                    rxFailure);
            }
        }

        if (!_satelliteMode || uplinkHz <= 0)
            return;

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
    /// One-shot: centre each slice's panadapter on its band frequency after pass init.
    /// Continuous Doppler uses autopan=0 so these pans are not yanked again.
    /// </summary>
    public void CenterBandPanadapters(long downlinkHz, long uplinkHz)
    {
        if (!_client.IsConnected)
            return;

        if (downlinkHz > 0)
            CenterSlicePan(_rxSliceIndex, downlinkHz, "RX");

        if (_satelliteMode && uplinkHz > 0)
            CenterSlicePan(_txSliceIndex, uplinkHz, "TX");

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

    private void CenterSlicePan(int sliceIndex, long centerHz, string role)
    {
        var panId = _client.GetSlicePanStreamId(sliceIndex);
        if (string.IsNullOrWhiteSpace(panId))
        {
            Log.Debug(
                "FlexRadio skip pan centre for {Role} slice {SliceIndex}: pan stream id not yet known",
                role,
                sliceIndex);
            return;
        }

        if (!_client.SetPanCenter(panId, centerHz, out var failure))
        {
            Log.Warning(
                "FlexRadio failed to centre {Role} pan {PanStreamId} at {CenterHz} Hz (slice {SliceIndex}): detail={Detail}",
                role,
                panId,
                centerHz,
                sliceIndex,
                failure);
        }
        else
        {
            Log.Information(
                "FlexRadio centred {Role} pan {PanStreamId} at {CenterMhz:0.###} MHz (slice {SliceIndex})",
                role,
                panId,
                FlexSmartSdrCodec.HzToMhz(centerHz),
                sliceIndex);
        }
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

        if (!_client.SetSliceTone(_txSliceIndex, _toneOn, _toneHz))
        {
            Log.Warning(
                "FlexRadio failed to confirm CTCSS on TX slice {SliceIndex}: enabled={Enabled}, toneHz={ToneHz}",
                _txSliceIndex,
                _toneOn,
                _toneHz);
        }
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
        var slices = _client.GetInUseSlices();
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

            return _rxSliceIndex != _txSliceIndex;
        }

        if (slices.Count == 1)
        {
            _rxSliceIndex = slices[0].Index;
            var createHz = _lastSubHz > 0 ? _lastSubHz : 435_000_000;
            var created = _client.CreateSlice(
                createHz,
                "USB",
                FlexAntennaPortResolver.ResolveTxPort(_antennaPortSettings, createHz));
            if (created is null || created.Value == _rxSliceIndex)
                return false;

            _txSliceIndex = created.Value;
            return _client.GetInUseSlices().Select(s => s.Index).Distinct().Count() >= 2;
        }

        var rxCreateHz = _lastMainHz > 0 ? _lastMainHz : 145_900_000;
        var txCreateHz = _lastSubHz > 0 ? _lastSubHz : 435_000_000;
        var rxCreated = _client.CreateSlice(
            rxCreateHz,
            "USB",
            FlexAntennaPortResolver.ResolveRxPort(_antennaPortSettings, rxCreateHz));
        var txCreated = _client.CreateSlice(
            txCreateHz,
            "USB",
            FlexAntennaPortResolver.ResolveTxPort(_antennaPortSettings, txCreateHz));
        if (rxCreated is null || txCreated is null || rxCreated.Value == txCreated.Value)
            return false;

        _rxSliceIndex = rxCreated.Value;
        _txSliceIndex = txCreated.Value;
        return _client.GetInUseSlices().Select(s => s.Index).Distinct().Count() >= 2;
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

    private static int ResolveTimeoutMs(int catDelayMs) =>
        Math.Max(500, catDelayMs * 20);
}

public sealed class FlexSatelliteSetupException : InvalidOperationException
{
    public FlexSatelliteSetupException(string message)
        : base(message)
    {
    }
}
