using System.Collections.ObjectModel;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private double _rigPassbandDownlinkAdjustKHz;
    private double _rigPassbandUplinkAdjustKHz;

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

    [ObservableProperty]
    private bool _showOperatingStyleRow;

    [ObservableProperty]
    private bool _isCwUplink;

    [ObservableProperty]
    private string _operatingStyleHint = "";

    public ObservableCollection<CtcssToneOption> CtcssToneOptions { get; } = [];

    [ObservableProperty]
    private double _receiveOffsetKHz;

    [ObservableProperty]
    private string _offsetAppliedHint = "";

    [ObservableProperty]
    private double _overlayX = 12;

    [ObservableProperty]
    private double _overlayY = 12;

    [ObservableProperty]
    private bool _isCollapsed;

    [ObservableProperty]
    private string _collapsedSummaryText = "—";

    public double OverlayMinWidth => IsCollapsed ? 220 : 380;

    public double OverlayMaxWidth => IsCollapsed ? 720 : 520;

    public string CollapseToggleGlyph => IsCollapsed ? "▶" : "▼";

    public string CollapseToggleToolTip => IsCollapsed ? "Expand frequency panel" : "Collapse to compact view";

    public bool ShowHeaderOperatingStyle => ShowOperatingStyleRow && !IsCollapsed;

    public bool IsVoiceOperatingStyleSelected => ShowOperatingStyleRow && !IsCwUplink;

    public bool IsCwOperatingStyleSelected => ShowOperatingStyleRow && IsCwUplink;

    [ObservableProperty]
    private SatelliteTransponderMode? _selectedMode;

    public ObservableCollection<SatelliteTransponderMode> AvailableModes { get; } = [];

    public FrequencyOverlayViewModel(ISettingsService settings, ISatelliteDatabaseService database)
    {
        _settings = settings;
        _database = database;
        OverlayX = settings.Current.FrequencyOverlayX;
        OverlayY = settings.Current.FrequencyOverlayY;
        IsCollapsed = settings.Current.FrequencyOverlayCollapsed;
    }

    [RelayCommand]
    private void ToggleCollapse()
    {
        IsCollapsed = !IsCollapsed;
    }

    [RelayCommand]
    private void SelectVoiceOperatingStyle() => SetCwUplink(false);

    [RelayCommand]
    private void SelectCwOperatingStyle() => SetCwUplink(true);

    [RelayCommand]
    private void ToggleCwUplink() => SetCwUplink(!IsCwUplink);

    public void SetCwUplink(bool cwUplink)
    {
        if (!ShowOperatingStyleRow || IsCwUplink == cwUplink)
            return;

        IsCwUplink = cwUplink;
    }

    partial void OnIsCollapsedChanged(bool value)
    {
        _settings.Current.FrequencyOverlayCollapsed = value;
        _ = _settings.SaveAsync();
        OnPropertyChanged(nameof(CollapseToggleGlyph));
        OnPropertyChanged(nameof(CollapseToggleToolTip));
        OnPropertyChanged(nameof(OverlayMinWidth));
        OnPropertyChanged(nameof(OverlayMaxWidth));
        OnPropertyChanged(nameof(ShowHeaderOperatingStyle));
        NotifyOperatingStyleSelectionChanged();
        RequestOverlayReclamp();
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
            _rigPassbandDownlinkAdjustKHz = 0;
            _rigPassbandUplinkAdjustKHz = 0;
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
        ApplyCorrectedDisplay(ComputeCorrected(_lastRangeRateKmPerSec));
    }

    private CorrectedFrequencies ComputeCorrected(double rangeRateKmPerSec) =>
        DopplerFrequencyCalculator.Compute(
            SelectedMode!,
            rangeRateKmPerSec,
            ReceiveOffsetKHz,
            _rigPassbandDownlinkAdjustKHz,
            _rigPassbandUplinkAdjustKHz);

    public CloudlogRadioUpdate? TryBuildCloudlogUpdate(SatelliteTrackState? state)
    {
        if (state is null || SelectedMode is null)
            return null;

        var rangeRate = state.LookAngles?.RangeRateKmPerSec ?? _lastRangeRateKmPerSec;
        var corrected = ComputeCorrected(rangeRate);

        return CloudlogRadioMapper.TryCreate(
            state.Name,
            SelectedMode,
            corrected,
            IsCwUplink,
            CwKeepSidebandDownlink);
    }

    public RigTrackingContext? TryBuildRigTrackingContext(SatelliteTrackState? state)
    {
        if (state is null || SelectedMode is null || state.LookAngles is null)
            return null;

        // Overlay modes reload when NoradId changes; don't drive CAT until synced.
        if (!string.Equals(state.NoradId, _currentNoradId, StringComparison.Ordinal))
            return null;

        var corrected = ComputeCorrected(state.LookAngles.RangeRateKmPerSec);

        return new RigTrackingContext
        {
            TrackState = state,
            Mode = SelectedMode,
            Corrected = corrected,
            TransmitOffsetKHz = 0,
            ReceiveOffsetKHz = ReceiveOffsetKHz,
            SelectedCtcssHz = GetActiveCtcssHz(),
            CwUplink = IsCwUplink,
            CwKeepSidebandDownlink = CwKeepSidebandDownlink
        };
    }

    private bool CwKeepSidebandDownlink =>
        _settings.Current.Rig?.CwKeepSidebandDownlink == true;

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
    /// <remarks>Event arg is <c>true</c> when transponder mode changed (full pass re-init); <c>false</c> for RX offset edits only.</remarks>
    public event EventHandler<bool>? OffsetsChanged;

    public event EventHandler? CtcssChanged;

    private void RequestOverlayReclamp() => OverlayLayoutChanged?.Invoke(this, EventArgs.Empty);

    partial void OnSelectedModeChanged(SatelliteTransponderMode? value)
    {
        if (_isLoadingSelection || string.IsNullOrEmpty(_currentSatelliteName))
            return;

        _rigPassbandDownlinkAdjustKHz = 0;
        _rigPassbandUplinkAdjustKHz = 0;

        if (_currentSatelliteName is not null)
        {
            ApplyOperatingStyleForSelectedMode();
            UpdateCtcssDisplay(restoreToneRole: GetOrCreateSelection().CtcssToneRole);
            ApplyOffsetsForSelectedMode();
            UpdateFromCurrentState();
        }

        if (value is not null)
            PersistSelection();

        OffsetsChanged?.Invoke(this, true);
        RequestOverlayReclamp();
        StoreOffsetCommand.NotifyCanExecuteChanged();
    }

    partial void OnReceiveOffsetKHzChanged(double value)
    {
        if (_isLoadingSelection)
            return;

        ApplyOffsetEdit();
    }

    /// <summary>Called when RX offset changes (spinner or step buttons).</summary>
    public void ApplyOffsetEdit()
    {
        RefreshFrequencyDisplay();
        OffsetsChanged?.Invoke(this, false);
    }

    /// <summary>Receive offset nudge in Hz (applied to downlink nominal before doppler).</summary>
    public void AdjustReceiveOffsetHz(int deltaHz)
    {
        const double maxKHz = 5.0;
        ReceiveOffsetKHz = Math.Clamp(ReceiveOffsetKHz + deltaHz / 1000.0, -maxKHz, maxKHz);
    }

    [RelayCommand(CanExecute = nameof(CanStoreOffset))]
    private void StoreOffset()
    {
        if (SelectedMode is null)
            return;

        var selection = GetOrCreateSelection();
        selection.RememberOffsets = true;
        selection.SetReceiveOffsetForMode(SelectedMode.Type, ReceiveOffsetKHz, UseCwReceiveOffsetStorage());
        _ = _settings.SaveAsync();

        var hz = (int)Math.Round(ReceiveOffsetKHz * 1000.0);
        var style = UseCwReceiveOffsetStorage() ? "CW " : "";
        OffsetAppliedHint = hz == 0
            ? $"Stored {style}offset cleared."
            : $"Stored {style}{hz} Hz offset.";
    }

    private bool CanStoreOffset() => HasTransponderData && SelectedMode is not null;

    partial void OnShowOperatingStyleRowChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowHeaderOperatingStyle));
        NotifyOperatingStyleSelectionChanged();
    }

    partial void OnHasTransponderDataChanged(bool value)
    {
        StoreOffsetCommand.NotifyCanExecuteChanged();
        UpdateCollapsedSummaryText();
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
        ApplyCorrectedDisplay(ComputeCorrected(_lastRangeRateKmPerSec));
        UpdateOffsetAppliedHint();
    }

    /// <summary>Main-dial passband trim from rig while tracking (updates downlink/uplink nominals).</summary>
    public void SyncRigPassbandAdjustments(double downlinkAdjustKHz, double uplinkAdjustKHz)
    {
        if (Math.Abs(_rigPassbandDownlinkAdjustKHz - downlinkAdjustKHz) < 0.0001
            && Math.Abs(_rigPassbandUplinkAdjustKHz - uplinkAdjustKHz) < 0.0001)
            return;

        _rigPassbandDownlinkAdjustKHz = downlinkAdjustKHz;
        _rigPassbandUplinkAdjustKHz = uplinkAdjustKHz;
        RefreshFrequencyDisplay();
    }

    private void ApplyCorrectedDisplay(CorrectedFrequencies corrected)
    {
        IsBeaconOnly = SelectedMode?.IsBeaconOnly == true;
        RadioTransmitText = IsBeaconOnly ? "—" : FrequencyDisplayFormat.FormatMHz(corrected.RadioTransmitKHz);
        RadioReceiveText = FrequencyDisplayFormat.FormatMHz(corrected.RadioReceiveKHz);
        SatelliteTransmitText = IsBeaconOnly ? "—" : FrequencyDisplayFormat.FormatMHz(corrected.SatelliteTransmitKHz);
        SatelliteReceiveText = FrequencyDisplayFormat.FormatMHz(corrected.SatelliteReceiveKHz);
        DopplerShiftText = FrequencyDisplayFormat.FormatDopplerKHz(corrected.DopplerShiftKHz);
        UpdateCollapsedSummaryText(corrected);
    }

    private void UpdateCollapsedSummaryText(CorrectedFrequencies? corrected = null)
    {
        if (!HasTransponderData || SelectedMode is null)
        {
            CollapsedSummaryText = string.IsNullOrEmpty(SatelliteName) || SatelliteName == "—"
                ? "—"
                : $"{SatelliteName} · no transponder data";
            return;
        }

        var mode = SelectedMode.DisplayLabel;
        if (IsCwUplink && ShowOperatingStyleRow)
            mode += " · CW";

        if (corrected is null)
        {
            CollapsedSummaryText = SatelliteName;
            return;
        }

        if (IsBeaconOnly)
        {
            CollapsedSummaryText =
                $"{SatelliteName} · {mode} · {FrequencyDisplayFormat.FormatMHzCompact(corrected.RadioReceiveKHz)}";
            return;
        }

        CollapsedSummaryText =
            $"{SatelliteName} · {mode} · {FrequencyDisplayFormat.FormatMHzCompact(corrected.RadioTransmitKHz)} / {FrequencyDisplayFormat.FormatMHzCompact(corrected.RadioReceiveKHz)}";
    }

    private void UpdateOffsetAppliedHint()
    {
        if (SelectedMode is null)
        {
            OffsetAppliedHint = "";
            return;
        }

        var isRev = SelectedMode.DopplerCorrection == DopplerCorrection.Reverse;
        if (Math.Abs(ReceiveOffsetKHz) > 0.0001)
        {
            var offset = $"{ReceiveOffsetKHz:+0.000;-0.000;0} kHz";
            OffsetAppliedHint = isRev
                ? $"{offset} on downlink. Tune Main for uplink."
                : $"{offset} on downlink.";
            return;
        }

        OffsetAppliedHint = isRev
            ? "Downlink only. Tune Main for uplink."
            : "Offsets downlink only.";
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
            ApplyOperatingStyleForSelectedMode();
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
            var selection = GetOrCreateSelection();
            ReceiveOffsetKHz = selection.GetReceiveOffsetForMode(
                SelectedMode.Type,
                UseCwReceiveOffsetStorage());
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
        if (SelectedCtcssTone is not null)
            selection.CtcssToneRole = SelectedCtcssTone.Role;
        if (SelectedMode is not null)
            selection.SetCwUplinkForMode(SelectedMode.Type, IsCwUplink);
        _ = _settings.SaveAsync();
    }

    partial void OnIsCwUplinkChanged(bool value)
    {
        if (_isLoadingSelection)
            return;

        UpdateOperatingStyleHint();
        NotifyOperatingStyleSelectionChanged();
        ApplyOffsetsForSelectedMode();
        PersistSelection();
        RefreshFrequencyDisplay();
        OffsetsChanged?.Invoke(this, true);
        RequestOverlayReclamp();
    }

    private bool UseCwReceiveOffsetStorage() =>
        ShowOperatingStyleRow && IsCwUplink;

    private void ApplyOperatingStyleForSelectedMode()
    {
        if (SelectedMode is null)
        {
            ShowOperatingStyleRow = false;
            OperatingStyleHint = "";
            _isLoadingSelection = true;
            try
            {
                IsCwUplink = false;
            }
            finally
            {
                _isLoadingSelection = false;
            }

            return;
        }

        var show = TransponderOperatingModes.SupportsCwUplinkToggle(SelectedMode);
        ShowOperatingStyleRow = show;

        _isLoadingSelection = true;
        try
        {
            IsCwUplink = show && GetOrCreateSelection().GetCwUplinkForMode(SelectedMode.Type);
        }
        finally
        {
            _isLoadingSelection = false;
        }

        UpdateOperatingStyleHint();
        NotifyOperatingStyleSelectionChanged();
    }

    private void NotifyOperatingStyleSelectionChanged()
    {
        OnPropertyChanged(nameof(IsVoiceOperatingStyleSelected));
        OnPropertyChanged(nameof(IsCwOperatingStyleSelected));
    }

    private void UpdateOperatingStyleHint()
    {
        OperatingStyleHint = IsCwUplink && ShowOperatingStyleRow
            ? CwKeepSidebandDownlink
                ? "CW on uplink; receive stays USB/LSB. Ctrl+W to toggle."
                : "CW on uplink and downlink. Ctrl+W to toggle."
            : ShowOperatingStyleRow
                ? "Voice uses database modes. Ctrl+W toggles CW."
                : "";
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
        ShowOperatingStyleRow = false;
        IsCwUplink = false;
        OperatingStyleHint = "";
        OffsetAppliedHint = "";
        UpdateCollapsedSummaryText();
    }
}
