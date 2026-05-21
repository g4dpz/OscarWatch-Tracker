using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OscarWatch.Core.Display;
using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.ViewModels;

public partial class MutualPassViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly ITleService _tleService;
    private readonly TrackingOrchestrator _tracking;

    public ObservableCollection<MutualPassRow> Passes { get; } = [];

    [ObservableProperty]
    private string _localStationSummary = "";

    [ObservableProperty]
    private string _remoteOperatorLabel = "";

    [ObservableProperty]
    private string _remoteGridSquare = "";

    [ObservableProperty]
    private double _filterMinElevationDeg = 5;

    [ObservableProperty]
    private int _filterMinPassDurationMinutes = 2;

    [ObservableProperty]
    private int _filterMinMutualDurationMinutes = 1;

    [ObservableProperty]
    private int _filterPredictionHours = 48;

    [ObservableProperty]
    private string _statusText = "";

    public MutualPassViewModel(
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
        var local = _settings.Current.GroundStation;
        LocalStationSummary = $"{local.DisplayName} ({local.GridSquare})";

        FilterMinElevationDeg = _settings.Current.MinimumElevationDeg;
        FilterMinPassDurationMinutes = _settings.Current.PassFilterMinDurationMinutes;
        FilterMinMutualDurationMinutes = Math.Max(1, FilterMinPassDurationMinutes / 2);
        FilterPredictionHours = _settings.Current.PassPredictionHours;
    }

    [RelayCommand]
    private async Task RefreshPassesAsync()
    {
        var grid = RemoteGridSquare.Trim();
        if (grid.Length < 4)
        {
            StatusText = "Enter the remote operator's Maidenhead grid square (at least 4 characters).";
            return;
        }

        StatusText = "Computing mutual passes…";

        try
        {
            await _tleService.EnsureLoadedAsync();

            var (lat, lon) = MaidenheadGrid.ToLatLonCenter(grid);
            var remoteSite = new GroundStation
            {
                DisplayName = string.IsNullOrWhiteSpace(RemoteOperatorLabel)
                    ? grid.ToUpperInvariant()
                    : RemoteOperatorLabel.Trim(),
                LatitudeDeg = lat,
                LongitudeDeg = lon,
                AltitudeMetersAsl = 50,
                GridSquare = grid.ToUpperInvariant()
            };

            var localSite = _settings.Current.GroundStation;
            var passes = await _tracking.GetMutualPassesAsync(
                localSite,
                remoteSite,
                FilterMinElevationDeg,
                FilterPredictionHours,
                FilterMinPassDurationMinutes,
                FilterMinMutualDurationMinutes);

            Passes.Clear();
            foreach (var pass in passes)
                Passes.Add(MutualPassRow.From(pass, localSite.DisplayName, remoteSite.DisplayName));

            StatusText = passes.Count == 0
                ? $"No mutual passes in the next {FilterPredictionHours} h for {localSite.GridSquare} and {remoteSite.GridSquare}."
                : $"{passes.Count} mutual pass(es) in the next {FilterPredictionHours} h";
        }
        catch (ArgumentException)
        {
            StatusText = "Invalid Maidenhead grid square.";
        }
        catch (Exception ex)
        {
            StatusText = $"Mutual pass prediction failed: {ex.Message}";
        }
    }
}

public sealed class MutualPassRow
{
    public MutualPassInfo Source { get; init; } = null!;
    public string SatelliteName { get; init; } = "";
    public string MutualWindowLine { get; init; } = "";
    public string OverlapDuration { get; init; } = "";
    public string LocalMaxEl { get; init; } = "";
    public string RemoteMaxEl { get; init; } = "";
    public string LocalPassLine { get; init; } = "";
    public string RemotePassLine { get; init; } = "";

    public static MutualPassRow From(MutualPassInfo pass, string localLabel, string remoteLabel)
    {
        return new()
        {
            Source = pass,
            SatelliteName = pass.SatelliteName,
            MutualWindowLine = PassDisplayFormat.FormatMutualWindowLine(
                pass.MutualStartUtc, pass.MutualEndUtc),
            OverlapDuration = PassDisplayFormat.FormatDurationMinutes(pass.Duration),
            LocalMaxEl = $"{pass.LocalPass.MaxElevationDeg:F1}°",
            RemoteMaxEl = $"{pass.RemotePass.MaxElevationDeg:F1}°",
            LocalPassLine = $"{localLabel}: {PassDisplayFormat.FormatPlannerAosLosLine(pass.LocalPass.AosUtc, pass.LocalPass.LosUtc)}",
            RemotePassLine = $"{remoteLabel}: {PassDisplayFormat.FormatPlannerAosLosLine(pass.RemotePass.AosUtc, pass.RemotePass.LosUtc)}"
        };
    }
}
