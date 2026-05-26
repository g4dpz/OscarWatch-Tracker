using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OscarWatch.Core.Export;
using OscarWatch.Core.Geo;
using OscarWatch.Core.Display;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.ViewModels;

public partial class PassPlanningViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly ITleService _tleService;
    private readonly TrackingOrchestrator _tracking;
    private bool _isSynchronizing;

    public ObservableCollection<StationProfile> Stations { get; } = [];
    public ObservableCollection<PassPlanningPassRow> Passes { get; } = [];

    [ObservableProperty]
    private StationProfile? _selectedStation;

    [ObservableProperty]
    private string _stationDisplayName = "";

    [ObservableProperty]
    private double _stationLatitudeDeg;

    [ObservableProperty]
    private double _stationLongitudeDeg;

    [ObservableProperty]
    private double _stationAltitudeMeters;

    [ObservableProperty]
    private string _stationGridSquare = "";

    [ObservableProperty]
    private double _filterMinElevationDeg = 5;

    [ObservableProperty]
    private int _filterMinDurationMinutes = 2;

    [ObservableProperty]
    private int _filterPredictionHours = 48;

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private bool _canDeleteStation;

    [ObservableProperty]
    private bool _useUtcTime;

    public PassPlanningViewModel(
        ISettingsService settings,
        ITleService tleService,
        TrackingOrchestrator tracking)
    {
        _settings = settings;
        _tleService = tleService;
        _tracking = tracking;
    }

    public void Initialize()
    {
        _settings.EnsureSavedStations();
        Stations.Clear();
        foreach (var station in _settings.Current.SavedStations)
            Stations.Add(station);

        FilterMinElevationDeg = _settings.Current.MinimumElevationDeg;
        FilterMinDurationMinutes = _settings.Current.PassFilterMinDurationMinutes;
        FilterPredictionHours = _settings.Current.PassPredictionHours;
        UseUtcTime = _settings.Current.PassPlannerUseUtcTime;

        SelectedStation = Stations.FirstOrDefault(s => s.Id == _settings.Current.ActiveStationId)
            ?? Stations.FirstOrDefault();
        UpdateCanDeleteStation();
    }

    partial void OnSelectedStationChanged(StationProfile? value)
    {
        if (value is null)
            return;

        _isSynchronizing = true;
        try
        {
            StationDisplayName = value.DisplayName;
            StationLatitudeDeg = value.LatitudeDeg;
            StationLongitudeDeg = value.LongitudeDeg;
            StationAltitudeMeters = value.AltitudeMetersAsl;
            StationGridSquare = value.GridSquare;
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    private void ApplyEditableFieldsToSelectedStation()
    {
        if (SelectedStation is null)
            return;

        SelectedStation.DisplayName = StationDisplayName;
        SelectedStation.LatitudeDeg = StationLatitudeDeg;
        SelectedStation.LongitudeDeg = StationLongitudeDeg;
        SelectedStation.AltitudeMetersAsl = StationAltitudeMeters;
        SelectedStation.GridSquare = StationGridSquare.Trim();
    }

    partial void OnStationLatitudeDegChanged(double value)
    {
        if (_isSynchronizing)
            return;

        _isSynchronizing = true;
        try
        {
            var grid = MaidenheadGrid.FromLatLon(StationLatitudeDeg, StationLongitudeDeg);
            if (!string.Equals(StationGridSquare, grid, StringComparison.Ordinal))
                StationGridSquare = grid;
        }
        finally
        {
            _isSynchronizing = false;
        }

        SyncStationFromEditableFields();
    }

    partial void OnStationLongitudeDegChanged(double value)
    {
        if (_isSynchronizing)
            return;

        _isSynchronizing = true;
        try
        {
            var grid = MaidenheadGrid.FromLatLon(StationLatitudeDeg, StationLongitudeDeg);
            if (!string.Equals(StationGridSquare, grid, StringComparison.Ordinal))
                StationGridSquare = grid;
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    partial void OnStationDisplayNameChanged(string value) => SyncStationFromEditableFields();
    partial void OnStationAltitudeMetersChanged(double value) => SyncStationFromEditableFields();

    partial void OnStationGridSquareChanged(string value)
    {
        if (_isSynchronizing || string.IsNullOrWhiteSpace(value) || value.Length < 4)
            return;

        _isSynchronizing = true;
        try
        {
            var (lat, lon) = MaidenheadGrid.ToLatLonCenter(value.Trim());
            if (!StationLatitudeDeg.Equals(lat))
                StationLatitudeDeg = lat;
            if (!StationLongitudeDeg.Equals(lon))
                StationLongitudeDeg = lon;
        }
        catch
        {
            // invalid grid
        }
        finally
        {
            _isSynchronizing = false;
        }

        SyncStationFromEditableFields();
    }

    private void SyncStationFromEditableFields()
    {
        if (_isSynchronizing)
            return;

        ApplyEditableFieldsToSelectedStation();
    }

    [RelayCommand]
    private void AddStation()
    {
        var home = Stations.FirstOrDefault();
        var profile = new StationProfile
        {
            DisplayName = $"Portable {Stations.Count + 1}",
            LatitudeDeg = home?.LatitudeDeg ?? 51.5,
            LongitudeDeg = home?.LongitudeDeg ?? -0.1,
            AltitudeMetersAsl = home?.AltitudeMetersAsl ?? 50,
            GridSquare = home?.GridSquare ?? "IO91wm"
        };
        _settings.Current.SavedStations.Add(profile);
        Stations.Add(profile);
        SelectedStation = profile;
        UpdateCanDeleteStation();
    }

    [RelayCommand]
    private void DeleteStation()
    {
        if (SelectedStation is null || Stations.Count <= 1)
            return;

        var removed = SelectedStation;
        _settings.Current.SavedStations.Remove(removed);
        Stations.Remove(removed);

        if (_settings.Current.ActiveStationId == removed.Id)
            _settings.Current.ActiveStationId = Stations[0].Id;

        SelectedStation = Stations[0];
        UpdateCanDeleteStation();
    }

    private void UpdateCanDeleteStation() => CanDeleteStation = Stations.Count > 1;

    partial void OnUseUtcTimeChanged(bool value)
    {
        OnPropertyChanged(nameof(TimeDisplayIndex));
        _settings.Current.PassPlannerUseUtcTime = value;
        RefreshPassDisplayTimes();
    }

    public int TimeDisplayIndex
    {
        get => UseUtcTime ? 1 : 0;
        set
        {
            if (value is not (0 or 1) || UseUtcTime == (value == 1))
                return;

            UseUtcTime = value == 1;
        }
    }

    private void RefreshPassDisplayTimes()
    {
        if (Passes.Count == 0)
            return;

        var rows = Passes.ToList();
        Passes.Clear();
        foreach (var row in rows)
            Passes.Add(PassPlanningPassRow.From(row.Source, UseUtcTime));
    }

    [RelayCommand]
    private async Task RefreshPassesAsync()
    {
        ApplyEditableFieldsToSelectedStation();
        StatusText = "Computing passes…";

        try
        {
            await _tleService.EnsureLoadedAsync();
            var site = SelectedStation?.ToGroundStation() ?? _settings.Current.GroundStation;
            var passes = await _tracking.GetPassesAsync(
                site,
                FilterMinElevationDeg,
                FilterPredictionHours,
                FilterMinDurationMinutes);

            Passes.Clear();
            foreach (var pass in passes)
                Passes.Add(PassPlanningPassRow.From(pass, UseUtcTime));

            StatusText = $"{Passes.Count} pass(es) in the next {FilterPredictionHours} h";
        }
        catch (Exception ex)
        {
            StatusText = $"Pass prediction failed: {ex.Message}";
        }
    }

    public async Task<bool> ExportSatelliteIcsAsync(Window owner, PassPlanningPassRow row)
    {
        var passInfos = Passes
            .Where(p => p.Source.NoradId == row.Source.NoradId)
            .Select(p => p.Source)
            .ToList();

        if (passInfos.Count == 0)
        {
            StatusText = "Refresh passes before exporting.";
            return false;
        }

        var storage = TopLevel.GetTopLevel(owner)?.StorageProvider;
        if (storage is null)
            return false;

        var safeName = SanitizeFileName(row.SatelliteName);
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Export {row.SatelliteName} passes",
            SuggestedFileName = $"oscarwatch-{safeName}.ics",
            DefaultExtension = "ics",
            FileTypeChoices =
            [
                new FilePickerFileType("iCalendar") { Patterns = ["*.ics"] }
            ]
        });

        if (file is null)
            return false;

        ApplyEditableFieldsToSelectedStation();
        var site = SelectedStation?.ToGroundStation() ?? _settings.Current.GroundStation;
        var ics = IcsPassExporter.BuildCalendar(
            passInfos,
            site,
            $"OscarWatch — {row.SatelliteName} @ {site.DisplayName}");

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(ics);

        StatusText = $"Exported {passInfos.Count} {row.SatelliteName} pass(es)";
        return true;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var sanitized = new string(chars).Trim();
        return string.IsNullOrEmpty(sanitized) ? "satellite" : sanitized;
    }

    public async Task SaveFiltersAndStationsAsync()
    {
        ApplyEditableFieldsToSelectedStation();
        _settings.Current.MinimumElevationDeg = FilterMinElevationDeg;
        _settings.Current.PassFilterMinDurationMinutes = FilterMinDurationMinutes;
        _settings.Current.PassPredictionHours = FilterPredictionHours;
        _settings.Current.PassPlannerUseUtcTime = UseUtcTime;
        await _settings.SaveAsync();
    }

    public async Task ApplyAsActiveStationAsync()
    {
        if (SelectedStation is null)
            return;

        ApplyEditableFieldsToSelectedStation();
        _settings.Current.ActiveStationId = SelectedStation.Id;
        _settings.ApplyActiveStation();
        _settings.Current.MinimumElevationDeg = FilterMinElevationDeg;
        _settings.Current.PassFilterMinDurationMinutes = FilterMinDurationMinutes;
        _settings.Current.PassPredictionHours = FilterPredictionHours;
        _settings.Current.PassPlannerUseUtcTime = UseUtcTime;
        await _settings.SaveAsync();
    }
}

public sealed class PassPlanningPassRow
{
    public PassInfo Source { get; init; } = null!;
    public string SatelliteName { get; init; } = "";
    public string AosLocal { get; init; } = "";
    public string LosLocal { get; init; } = "";
    public string TcaLocal { get; init; } = "";
    public string MaxEl { get; init; } = "";
    public string Duration { get; init; } = "";
    public string AzimuthSummary { get; init; } = "";
    public string AosLosLine { get; init; } = "";

    public static PassPlanningPassRow From(PassInfo p, bool useUtc = false)
    {
        var aosLosLine = PassDisplayFormat.FormatPlannerAosLosLine(p.AosUtc, p.LosUtc, useUtc: useUtc);
        var (aos, los) = PassDisplayFormat.FormatLocalTimes(p.AosUtc, p.LosUtc, useUtc: useUtc);
        var tca = PassDisplayFormat.FormatPlannerTca(p.MaxElevationUtc, p.AosUtc, useUtc: useUtc);
        var az = $"{p.AosAzimuthDeg:F0}°→{p.LosAzimuthDeg:F0}°";

        return new()
        {
            Source = p,
            SatelliteName = p.SatelliteName,
            AosLocal = aos,
            LosLocal = los,
            TcaLocal = tca,
            MaxEl = $"{p.MaxElevationDeg:F1}°",
            Duration = p.Duration.ToString(@"mm\:ss"),
            AzimuthSummary = az,
            AosLosLine = aosLosLine
        };
    }
}
