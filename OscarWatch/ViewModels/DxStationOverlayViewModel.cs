using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Core.Services;

namespace OscarWatch.ViewModels;

public partial class DxStationOverlayViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly IOrbitPropagator _propagator;
    private GroundStation? _remoteSite;

    [ObservableProperty]
    private string _gridSquare = "";

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private GeoCoordinate? _remoteCoordinate;

    [ObservableProperty]
    private string _azimuthText = "—";

    [ObservableProperty]
    private string _elevationText = "—";

    [ObservableProperty]
    private string _collapsedSummaryText = "—";

    [ObservableProperty]
    private string _validationHint = "";

    [ObservableProperty]
    private double _overlayX = 12;

    [ObservableProperty]
    private double _overlayY = 56;

    [ObservableProperty]
    private bool _isCollapsed = true;

    [ObservableProperty]
    private bool _isPanelOpen;

    public double OverlayMinWidth => IsCollapsed ? 200 : 280;

    public double OverlayMaxWidth => IsCollapsed ? 640 : 360;

    public string CollapseToggleGlyph => IsCollapsed ? "▶" : "▼";

    public string CollapseToggleToolTip => IsCollapsed ? "Expand DX station panel" : "Collapse to compact view";

    public string MapLauncherToolTip => IsActive
        ? "Show DX station monitor"
        : "Enter remote station grid square";

    public Thickness OverlayMargin => new(OverlayX, OverlayY, 0, 0);

    public event EventHandler? OverlayLayoutChanged;

    public bool ShowValidationHint => !string.IsNullOrEmpty(ValidationHint);

    public DxStationOverlayViewModel(ISettingsService settings, IOrbitPropagator propagator)
    {
        _settings = settings;
        _propagator = propagator;
        GridSquare = settings.Current.RemoteStationGridSquare;
        OverlayX = settings.Current.DxOverlayX;
        OverlayY = settings.Current.DxOverlayY;
        IsCollapsed = settings.Current.DxOverlayCollapsed;
        ApplyGridSquare(GridSquare, persist: false);
        IsPanelOpen = false;
    }

    [RelayCommand]
    private void OpenFromMapIcon()
    {
        IsPanelOpen = true;
        IsCollapsed = IsActive;
        RequestOverlayReclamp();
    }

    [RelayCommand]
    private void ToggleCollapse() => IsCollapsed = !IsCollapsed;

    [RelayCommand]
    private void ClearTarget()
    {
        GridSquare = "";
        IsPanelOpen = false;
    }

    public void Update(SatelliteTrackState? focused)
    {
        if (!IsActive || _remoteSite is null)
        {
            AzimuthText = "—";
            ElevationText = "—";
            UpdateCollapsedSummaryText();
            return;
        }

        if (focused is null || !_propagator.HasSatellite(focused.NoradId))
        {
            AzimuthText = "—";
            ElevationText = "—";
            UpdateCollapsedSummaryText();
            return;
        }

        var look = _propagator.GetLookAngles(focused.NoradId, _remoteSite, DateTime.UtcNow);
        if (look.ElevationDeg <= 0)
        {
            AzimuthText = "—";
            ElevationText = "below horizon";
        }
        else
        {
            AzimuthText = $"{look.AzimuthDeg:F1}°";
            ElevationText = $"{look.ElevationDeg:F1}°";
        }

        UpdateCollapsedSummaryText();
    }

    partial void OnGridSquareChanged(string value)
    {
        ApplyGridSquare(value, persist: true);
        UpdateCollapsedSummaryText();
    }

    partial void OnIsActiveChanged(bool value) => OnPropertyChanged(nameof(MapLauncherToolTip));

    partial void OnValidationHintChanged(string value) => OnPropertyChanged(nameof(ShowValidationHint));

    partial void OnIsCollapsedChanged(bool value)
    {
        _settings.Current.DxOverlayCollapsed = value;
        _settings.RequestSave();
        OnPropertyChanged(nameof(CollapseToggleGlyph));
        OnPropertyChanged(nameof(CollapseToggleToolTip));
        OnPropertyChanged(nameof(OverlayMinWidth));
        OnPropertyChanged(nameof(OverlayMaxWidth));
        RequestOverlayReclamp();
    }

    partial void OnOverlayXChanged(double value) => OnPropertyChanged(nameof(OverlayMargin));

    partial void OnOverlayYChanged(double value) => OnPropertyChanged(nameof(OverlayMargin));

    public void SetOverlayPosition(double x, double y, bool persist = false)
    {
        OverlayX = x;
        OverlayY = y;
        OnPropertyChanged(nameof(OverlayMargin));
        if (persist)
        {
            _settings.Current.DxOverlayX = x;
            _settings.Current.DxOverlayY = y;
            _settings.RequestSave();
        }
    }

    public void PersistOverlayPosition() => _settings.RequestSave();

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

    private void ApplyGridSquare(string value, bool persist)
    {
        var wasActive = IsActive;
        var trimmed = value.Trim();
        if (trimmed.Length < 4)
        {
            IsActive = false;
            _remoteSite = null;
            RemoteCoordinate = null;
            ValidationHint = trimmed.Length == 0 ? "" : "Enter at least 4 characters (e.g. JO22 or JO22lk).";
            AzimuthText = "—";
            ElevationText = "—";
            if (persist)
            {
                _settings.Current.RemoteStationGridSquare = trimmed;
                _settings.RequestSave();
            }

            return;
        }

        try
        {
            var normalized = trimmed.ToUpperInvariant();
            var (lat, lon) = MaidenheadGrid.ToLatLonCenter(normalized);
            IsActive = true;
            RemoteCoordinate = new GeoCoordinate(lat, lon);
            ValidationHint = "";
            RebuildRemoteSite(normalized, lat, lon);
            if (!wasActive)
            {
                IsPanelOpen = true;
                IsCollapsed = true;
            }

            if (persist)
            {
                _settings.Current.RemoteStationGridSquare = normalized;
                _settings.RequestSave();
            }
        }
        catch (ArgumentException)
        {
            IsActive = false;
            _remoteSite = null;
            RemoteCoordinate = null;
            ValidationHint = "Invalid Maidenhead grid square.";
            AzimuthText = "—";
            ElevationText = "—";
        }
    }

    private void RebuildRemoteSite(string? normalizedGrid = null, double? lat = null, double? lon = null)
    {
        if (!IsActive || RemoteCoordinate is null)
        {
            _remoteSite = null;
            return;
        }

        var grid = normalizedGrid ?? GridSquare.Trim().ToUpperInvariant();
        var coordinate = RemoteCoordinate;
        _remoteSite = new GroundStation
        {
            DisplayName = grid,
            LatitudeDeg = lat ?? coordinate.LatitudeDeg,
            LongitudeDeg = lon ?? coordinate.LongitudeDeg,
            AltitudeMetersAsl = 50,
            GridSquare = grid
        };
    }

    private void UpdateCollapsedSummaryText()
    {
        if (!IsActive)
        {
            CollapsedSummaryText = "—";
            return;
        }

        var grid = GridSquare.Trim().ToUpperInvariant();
        if (ElevationText == "below horizon")
        {
            CollapsedSummaryText = $"{grid} · below horizon";
            return;
        }

        if (AzimuthText == "—")
        {
            CollapsedSummaryText = $"{grid} · Az — · El —";
            return;
        }

        CollapsedSummaryText = $"{grid} · Az {AzimuthText} · El {ElevationText}";
    }

    private void RequestOverlayReclamp() => OverlayLayoutChanged?.Invoke(this, EventArgs.Empty);
}
