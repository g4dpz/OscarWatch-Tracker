using System.Collections.ObjectModel;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using OscarWatch.Core.Cloudlog;
using OscarWatch.Core.Display;
using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using OscarWatch.Core.Services;

namespace OscarWatch.ViewModels;

public partial class FrequencyOverlayViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly ISatelliteDatabaseService _database;
    private string? _currentSatelliteName;
    private string? _currentStorageKey;
    private string? _currentNoradId;
    private bool _isLoadingSelection;
    private double _lastRangeRateKmPerSec;
    private SatelliteTrackState? _lastTrackState;

    [ObservableProperty]
    private string _satelliteName = "—";

    [ObservableProperty]
    private bool _hasTransponderData;

    [ObservableProperty]
    private bool _isBeaconOnly;

    [ObservableProperty]
    private bool _showBelowHorizonDim;

    [ObservableProperty]
    private string _radioTransmitText = "—";

    [ObservableProperty]
    private string _radioReceiveText = "—";

    [ObservableProperty]
    private string _satelliteTransmitText = "—";

    [ObservableProperty]
    private string _satelliteReceiveText = "—";

    [ObservableProperty]
    private string _dopplerShiftText = "";

    [ObservableProperty]
    private bool _showCtcssRow;

    [ObservableProperty]
    private bool _showCtcssSelector;

    [ObservableProperty]
    private bool _showCtcssStatic;

    [ObservableProperty]
    private string _selectedCtcssText = "";

    [ObservableProperty]
    private CtcssToneOption? _selectedCtcssTone;

    [ObservableProperty]
    private string _ctcssHintText = "";

    [ObservableProperty]
    private bool _showCtcssHint;

    public ObservableCollection<CtcssToneOption> CtcssToneOptions { get; } = [];

    [ObservableProperty]
    private double _transmitOffsetKHz;

    [ObservableProperty]
    private double _receiveOffsetKHz;

    [ObservableProperty]
    private bool _rememberOffsets = true;

    [ObservableProperty]
    private string _offsetAppliedHint = "";

    [ObservableProperty]
    private double _overlayX = 12;

    [ObservableProperty]
    private double _overlayY = 12;

    [ObservableProperty]
    private SatelliteTransponderMode? _selectedMode;

    public ObservableCollection<SatelliteTransponderMode> AvailableModes { get; } = [];

    public FrequencyOverlayViewModel(ISettingsService settings, ISatelliteDatabaseService database)
    {
        _settings = settings;
        _database = database;
        OverlayX = settings.Current.FrequencyOverlayX;
        OverlayY = settings.Current.FrequencyOverlayY;
    }

    /// <summary>Keep the full panel inside the map area (called after measure / resize).</summary>
    public void EnsureOverlayWithinHost(double hostWidth, double hostHeight, double overlayWidth, double overlayHeight)
    {
        if (hostWidth <= 0 || hostHeight <= 0 || overlayWidth <= 0 || overlayHeight <= 0)
            return;

        const double edge = 8;
        var maxX = Math.Max(edge, hostWidth - overlayWidth - edge);
        var maxY = Math.Max(edge, hostHeight - overlayHeight - edge);
        var x = Math.Clamp(OverlayX, edge, maxX);
        var y = Math.Clamp(OverlayY, edge, maxY);
        if (Math.Abs(x - OverlayX) > 0.5 || Math.Abs(y - OverlayY) > 0.5)
            SetOverlayPosition(x, y, persist: true);
    }

    public void ReloadFromDatabase()
    {
        _database.Reload();
        if (string.IsNullOrEmpty(_currentSatelliteName))
            return;

        LoadModesForSatellite(_currentSatelliteName);
        if (_lastTrackState is not null)
            Update(_lastTrackState);
    }

    public void Update(SatelliteTrackState? state)
    {
        _lastTrackState = state;
        if (state is null)
        {
            SatelliteName = "—";
            _currentNoradId = null;
            _currentSatelliteName = null;
            _currentStorageKey = null;
            HasTransponderData = false;
            ClearFrequencyDisplay();
            return;
        }

        SatelliteName = state.Name;
        ShowBelowHorizonDim = state.LookAngles is null || state.LookAngles.ElevationDeg < 0;

        if (!string.Equals(_currentNoradId, state.NoradId, StringComparison.Ordinal))
        {
            _currentNoradId = state.NoradId;
            _currentSatelliteName = state.Name;
            _currentStorageKey = ResolveStorageKey(state.Name);
            LoadModesForSatellite(state.Name);
            RequestOverlayReclamp();
        }

        if (SelectedMode is null)
        {
            HasTransponderData = false;
            ClearFrequencyDisplay();
            return;
        }

        HasTransponderData = true;
        IsBeaconOnly = SelectedMode.IsBeaconOnly;

        _lastRangeRateKmPerSec = state.LookAngles?.RangeRateKmPerSec ?? 0;
        var corrected = DopplerFrequencyCalculator.Compute(
            SelectedMode,
            _lastRangeRateKmPerSec,
            TransmitOffsetKHz,
            ReceiveOffsetKHz);

        RadioTransmitText = IsBeaconOnly ? "—" : FrequencyDisplayFormat.FormatMHz(corrected.RadioTransmitKHz);
        RadioReceiveText = FrequencyDisplayFormat.FormatMHz(corrected.RadioReceiveKHz);
        SatelliteTransmitText = IsBeaconOnly ? "—" : FrequencyDisplayFormat.FormatMHz(corrected.SatelliteTransmitKHz);
        SatelliteReceiveText = FrequencyDisplayFormat.FormatMHz(corrected.SatelliteReceiveKHz);
        DopplerShiftText = FrequencyDisplayFormat.FormatDopplerKHz(corrected.DopplerShiftKHz);
    }

    public CloudlogRadioUpdate? TryBuildCloudlogUpdate(SatelliteTrackState? state)
    {
        if (state is null || SelectedMode is null)
            return null;

        var rangeRate = state.LookAngles?.RangeRateKmPerSec ?? _lastRangeRateKmPerSec;
        var corrected = DopplerFrequencyCalculator.Compute(
            SelectedMode,
            rangeRate,
            TransmitOffsetKHz,
            ReceiveOffsetKHz);

        return CloudlogRadioMapper.TryCreate(state.Name, SelectedMode, corrected);
    }

    public RigTrackingContext? TryBuildRigTrackingContext(SatelliteTrackState? state)
    {
        if (state is null || SelectedMode is null || state.LookAngles is null)
            return null;

        var corrected = DopplerFrequencyCalculator.Compute(
            SelectedMode,
            state.LookAngles.RangeRateKmPerSec,
            TransmitOffsetKHz,
            ReceiveOffsetKHz);

        return new RigTrackingContext
        {
            TrackState = state,
            Mode = SelectedMode,
            Corrected = corrected,
            TransmitOffsetKHz = TransmitOffsetKHz,
            ReceiveOffsetKHz = ReceiveOffsetKHz,
            SelectedCtcssHz = GetActiveCtcssHz()
        };
    }

    /// <summary>Hz sent to the uplink VFO (combo selection, or the mode's only tone).</summary>
    public double? GetActiveCtcssHz()
    {
        if (SelectedCtcssTone is { } selected)
            return selected.Hz;

        if (SelectedMode is null || !SelectedMode.HasAnyCtcss)
            return null;

        if (SelectedMode.HasCtcss)
            return SelectedMode.CtcssHz;

        return SelectedMode.CtcssArmHz;
    }

    public Thickness OverlayMargin => new(OverlayX, OverlayY, 0, 0);

    public void SetOverlayPosition(double x, double y, bool persist = false)
    {
        OverlayX = x;
        OverlayY = y;
        OnPropertyChanged(nameof(OverlayMargin));
        _settings.Current.FrequencyOverlayX = x;
        _settings.Current.FrequencyOverlayY = y;
        if (persist)
            _ = _settings.SaveAsync();
    }

    partial void OnOverlayXChanged(double value) => OnPropertyChanged(nameof(OverlayMargin));

    partial void OnOverlayYChanged(double value) => OnPropertyChanged(nameof(OverlayMargin));

    public void PersistOverlayPosition() => _ = _settings.SaveAsync();

    /// <summary>Height may change when modes load; view reclamps on next layout pass.</summary>
    public event EventHandler? OverlayLayoutChanged;

    /// <summary>TX/RX micro-adjust offsets changed — rig should refresh CAT promptly.</summary>
    public event EventHandler? OffsetsChanged;

    public event EventHandler? CtcssChanged;

    private void RequestOverlayReclamp() => OverlayLayoutChanged?.Invoke(this, EventArgs.Empty);

    partial void OnSelectedModeChanged(SatelliteTransponderMode? value)
    {
        if (_isLoadingSelection || string.IsNullOrEmpty(_currentSatelliteName))
            return;

        if (value is not null)
            PersistSelection();

        if (_currentSatelliteName is not null)
        {
            UpdateCtcssDisplay(restoreToneRole: GetOrCreateSelection().CtcssToneRole);
            ApplyOffsetsForSelectedMode();
            UpdateFromCurrentState();
        }

        RequestOverlayReclamp();
    }

    partial void OnTransmitOffsetKHzChanged(double value)
    {
        if (_isLoadingSelection)
            return;

        ApplyOffsetEdit();
    }

    partial void OnReceiveOffsetKHzChanged(double value)
    {
        if (_isLoadingSelection)
            return;

        ApplyOffsetEdit();
    }

    /// <summary>Called when offset spinners change (including while typing).</summary>
    public void ApplyOffsetEdit()
    {
        if (RememberOffsets)
            PersistSelection();

        RefreshFrequencyDisplay();
        OffsetsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Recompute Radio/Sat frequencies from last track state and current offsets.</summary>
    public void RefreshFrequencyDisplay()
    {
        if (SelectedMode is null)
        {
            OffsetAppliedHint = "";
            return;
        }

        _lastRangeRateKmPerSec = _lastTrackState?.LookAngles?.RangeRateKmPerSec ?? _lastRangeRateKmPerSec;
        var corrected = DopplerFrequencyCalculator.Compute(
            SelectedMode,
            _lastRangeRateKmPerSec,
            TransmitOffsetKHz,
            ReceiveOffsetKHz);

        IsBeaconOnly = SelectedMode.IsBeaconOnly;
        RadioTransmitText = IsBeaconOnly ? "—" : FrequencyDisplayFormat.FormatMHz(corrected.RadioTransmitKHz);
        RadioReceiveText = FrequencyDisplayFormat.FormatMHz(corrected.RadioReceiveKHz);
        SatelliteTransmitText = IsBeaconOnly ? "—" : FrequencyDisplayFormat.FormatMHz(corrected.SatelliteTransmitKHz);
        SatelliteReceiveText = FrequencyDisplayFormat.FormatMHz(corrected.SatelliteReceiveKHz);
        DopplerShiftText = FrequencyDisplayFormat.FormatDopplerKHz(corrected.DopplerShiftKHz);
        UpdateOffsetAppliedHint();
    }

    private void UpdateOffsetAppliedHint()
    {
        if (SelectedMode is null)
        {
            OffsetAppliedHint = "";
            return;
        }

        var parts = new List<string>();
        if (Math.Abs(TransmitOffsetKHz) > 0.0001 && !IsBeaconOnly)
            parts.Add($"TX {TransmitOffsetKHz:+0.000;-0.000;0} kHz → Radio ↑");
        if (Math.Abs(ReceiveOffsetKHz) > 0.0001)
            parts.Add($"RX {ReceiveOffsetKHz:+0.000;-0.000;0} kHz → Radio ↓");

        OffsetAppliedHint = parts.Count == 0
            ? "Offsets apply to the Radio row only (Sat shows nominal + doppler)."
            : string.Join(" · ", parts);
    }

    partial void OnRememberOffsetsChanged(bool value)
    {
        if (_isLoadingSelection)
            return;

        var selection = GetOrCreateSelection();
        selection.RememberOffsets = value;
        ApplyOffsetsForSelectedMode();
        PersistSelection();
        UpdateFromCurrentState();
        RequestOverlayReclamp();
    }

    partial void OnSelectedCtcssToneChanged(CtcssToneOption? value)
    {
        if (_isLoadingSelection)
            return;

        UpdateCtcssHint();
        PersistSelection();
        CtcssChanged?.Invoke(this, EventArgs.Empty);
        RequestOverlayReclamp();
    }

    private void LoadModesForSatellite(string name)
    {
        _isLoadingSelection = true;
        try
        {
            AvailableModes.Clear();
            SelectedMode = null;
            var entry = _database.TryGetEntry(name);
            if (entry is null || entry.Modes.Count == 0)
            {
                HasTransponderData = false;
                ClearFrequencyDisplay();
                return;
            }

            foreach (var mode in entry.Modes)
                AvailableModes.Add(mode);

            var selection = GetOrCreateSelection();
            SelectedMode = ResolveSelectedMode(selection);
            RememberOffsets = selection.RememberOffsets;
            ApplyOffsetsForSelectedMode();
            UpdateCtcssDisplay(restoreToneRole: selection.CtcssToneRole);
        }
        finally
        {
            _isLoadingSelection = false;
            RequestOverlayReclamp();
        }
    }

    private SatelliteTransponderMode? ResolveSelectedMode(SatelliteFrequencySelection selection)
    {
        if (AvailableModes.Count == 0)
            return null;

        if (selection.ModeIndex >= 0 && selection.ModeIndex < AvailableModes.Count)
            return AvailableModes[selection.ModeIndex];

        var byType = AvailableModes.FirstOrDefault(m =>
            m.Type.Equals(selection.ModeType, StringComparison.OrdinalIgnoreCase));
        return byType ?? AvailableModes[0];
    }

    private SatelliteFrequencySelection GetOrCreateSelection()
    {
        var key = _currentStorageKey ?? _currentSatelliteName ?? "";
        if (string.IsNullOrWhiteSpace(key))
            return new SatelliteFrequencySelection();

        if (_settings.Current.FrequencySelections.TryGetValue(key, out var existing))
            return existing;

        var created = new SatelliteFrequencySelection();
        _settings.Current.FrequencySelections[key] = created;
        return created;
    }

    private string ResolveStorageKey(string tleName)
    {
        var entry = _database.TryGetEntry(tleName);
        return entry?.Name ?? tleName.Trim();
    }

    private void ApplyOffsetsForSelectedMode()
    {
        if (SelectedMode is null)
            return;

        _isLoadingSelection = true;
        try
        {
            if (!RememberOffsets)
            {
                TransmitOffsetKHz = 0;
                ReceiveOffsetKHz = 0;
                return;
            }

            var selection = GetOrCreateSelection();
            var (tx, rx) = selection.GetOffsetsForMode(SelectedMode.Type);
            TransmitOffsetKHz = tx;
            ReceiveOffsetKHz = rx;
        }
        finally
        {
            _isLoadingSelection = false;
        }
    }

    private void PersistSelection()
    {
        if (string.IsNullOrEmpty(_currentStorageKey) && string.IsNullOrEmpty(_currentSatelliteName))
            return;
        if (SelectedMode is null)
            return;

        var selection = GetOrCreateSelection();
        selection.ModeType = SelectedMode.Type;
        selection.ModeIndex = AvailableModes.IndexOf(SelectedMode);
        if (selection.ModeIndex < 0)
            selection.ModeIndex = 0;
        selection.RememberOffsets = RememberOffsets;
        if (RememberOffsets)
            selection.SetOffsetsForMode(SelectedMode.Type, TransmitOffsetKHz, ReceiveOffsetKHz);
        if (SelectedCtcssTone is not null)
            selection.CtcssToneRole = SelectedCtcssTone.Role;
        _ = _settings.SaveAsync();
    }

    private void UpdateFromCurrentState() => RefreshFrequencyDisplay();

    private void UpdateCtcssDisplay(string? restoreToneRole = null)
    {
        CtcssToneOptions.Clear();
        SelectedCtcssTone = null;

        if (SelectedMode is null || !SelectedMode.HasAnyCtcss)
        {
            ShowCtcssRow = false;
            ShowCtcssSelector = false;
            ShowCtcssStatic = false;
            SelectedCtcssText = "";
            CtcssHintText = "";
            ShowCtcssHint = false;
            return;
        }

        ShowCtcssRow = true;

        if (SelectedMode.HasCtcss && SelectedMode.HasCtcssArm)
        {
            var access = new CtcssToneOption("access", "Access", SelectedMode.CtcssHz!.Value);
            var arm = new CtcssToneOption("arm", "Arm", SelectedMode.CtcssArmHz!.Value);
            CtcssToneOptions.Add(access);
            CtcssToneOptions.Add(arm);

            ShowCtcssSelector = true;
            ShowCtcssStatic = false;
            SelectedCtcssText = "";

            var role = restoreToneRole ?? GetOrCreateSelection().CtcssToneRole;
            SelectedCtcssTone = role.Equals("arm", StringComparison.OrdinalIgnoreCase) ? arm : access;
        }
        else
        {
            ShowCtcssSelector = false;
            ShowCtcssStatic = true;
            SelectedCtcssText = SelectedMode.HasCtcss
                ? SelectedMode.CtcssAccessDisplay
                : SelectedMode.CtcssArmDisplay;
        }

        UpdateCtcssHint();
    }

    private void UpdateCtcssHint()
    {
        if (SelectedMode is null || !SelectedMode.HasAnyCtcss)
        {
            CtcssHintText = "";
            ShowCtcssHint = false;
            return;
        }

        var armSelected = SelectedCtcssTone?.Role.Equals("arm", StringComparison.OrdinalIgnoreCase) == true
            || (!ShowCtcssSelector && SelectedMode.HasCtcssArm && !SelectedMode.HasCtcss);

        CtcssHintText = armSelected ? SelectedMode.CtcssHintLine : "";
        ShowCtcssHint = !string.IsNullOrEmpty(CtcssHintText);
    }

    private void ClearFrequencyDisplay()
    {
        RadioTransmitText = "—";
        RadioReceiveText = "—";
        SatelliteTransmitText = "—";
        SatelliteReceiveText = "—";
        DopplerShiftText = "";
        IsBeaconOnly = false;
        ShowCtcssRow = false;
        ShowCtcssSelector = false;
        ShowCtcssStatic = false;
        SelectedCtcssText = "";
        CtcssToneOptions.Clear();
        SelectedCtcssTone = null;
        CtcssHintText = "";
        ShowCtcssHint = false;
        OffsetAppliedHint = "";
    }
}
