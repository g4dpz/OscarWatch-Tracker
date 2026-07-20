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
    private bool _toneOn;
    private double _toneHz = 67.0;

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

        var hz = _client.GetSliceFrequencyHz(slice);
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

        StoreFrequencyHz(_currentVfo, hz);
        if (!_client.IsConnected)
            return true;

        return _client.TuneSlice(SliceFor(_currentVfo), hz);
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
        _satelliteMode = on;
        if (!_client.IsConnected)
            return;

        if (!on)
        {
            _client.SetFullDuplex(false);
            return;
        }

        if (!_client.SetFullDuplex(true))
            Log.Warning("Flex SmartSDR failed to enable full duplex");

        EnsureDuplexSlices();
        if (!_client.SetSliceTx(_txSliceIndex, true))
            Log.Warning("Flex SmartSDR failed to mark TX slice {Index}", _txSliceIndex);
    }

    public void ExchangeVfos()
    {
        // Slice roles stay RX/TX; front-panel / SmartSDR swap is not mirrored.
        (_rxSliceIndex, _txSliceIndex) = (_txSliceIndex, _rxSliceIndex);
        (_lastMainHz, _lastSubHz) = (_lastSubHz, _lastMainHz);
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

        _client.SetSliceTone(_txSliceIndex, _toneOn, _toneHz);
    }

    private void EnsureDuplexSlices()
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

            return;
        }

        if (slices.Count == 1)
        {
            _rxSliceIndex = slices[0].Index;
            var created = _client.CreateSlice(
                _lastSubHz > 0 ? _lastSubHz : 435_000_000,
                "USB");
            _txSliceIndex = created ?? (_rxSliceIndex == 0 ? 1 : 0);
            return;
        }

        var rxCreated = _client.CreateSlice(
            _lastMainHz > 0 ? _lastMainHz : 145_900_000,
            "USB");
        var txCreated = _client.CreateSlice(
            _lastSubHz > 0 ? _lastSubHz : 435_000_000,
            "USB");
        _rxSliceIndex = rxCreated ?? 0;
        _txSliceIndex = txCreated ?? 1;
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
