using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using OscarWatch.Core.Display;
using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
using OscarWatch.Localization;

namespace OscarWatch.ViewModels;

public partial class MutualPassViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly ITleService _tleService;
    private readonly TrackingOrchestrator _tracking;
    private readonly ILocalizationService _l;

    public IReadOnlyList<string> TimeDisplayLabels { get; }

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

    [ObservableProperty]
    private bool _useUtcTime;

    private string _lastLocalLabel = "";
    private string _lastRemoteLabel = "";
    private GroundStation? _lastLocalSite;
    private GroundStation? _lastRemoteSite;

    public MutualPassViewModel(
        ISettingsService settings,
        ITleService tleService,
        TrackingOrchestrator tracking,
        ILocalizationService localization)
    {
        _settings = settings;
        _tleService = tleService;
        _tracking = tracking;
        _l = localization;
        TimeDisplayLabels =
        [
            _l.Get("Pass.Time.Local"),
            _l.Get("Pass.Time.Utc")
        ];
    }

    public void Initialize()
    {
        var local = _settings.Current.GroundStation;
        LocalStationSummary = $"{local.DisplayName} ({local.GridSquare})";

        FilterMinElevationDeg = _settings.Current.MinimumElevationDeg;
        FilterMinPassDurationMinutes = _settings.Current.PassFilterMinDurationMinutes;
        FilterMinMutualDurationMinutes = Math.Max(1, FilterMinPassDurationMinutes / 2);
        FilterPredictionHours = _settings.Current.PassPredictionHours;
        UseUtcTime = _settings.Current.PassPlannerUseUtcTime;
    }

    partial void OnUseUtcTimeChanged(bool value)
    {
        OnPropertyChanged(nameof(TimeDisplayIndex));
        _settings.Current.PassPlannerUseUtcTime = value;
        RefreshPassDisplayTimes();
        _settings.RequestSave();
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
            Passes.Add(MutualPassRow.From(
                row.Source,
                _lastLocalLabel,
                _lastRemoteLabel,
                UseUtcTime,
                _settings.Current.Use24HourClock));
    }

    [RelayCommand]
    private async Task RefreshPassesAsync()
    {
        var grid = RemoteGridSquare.Trim();
        if (grid.Length < 4)
        {
            StatusText = _l.Get("Mutual.Status.EnterGrid");
            return;
        }

        StatusText = _l.Get("Pass.ComputingMutual");

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

            _lastLocalLabel = localSite.DisplayName;
            _lastRemoteLabel = remoteSite.DisplayName;
            _lastLocalSite = localSite;
            _lastRemoteSite = remoteSite;

            Passes.Clear();
            foreach (var pass in passes)
                Passes.Add(MutualPassRow.From(
                    pass,
                    _lastLocalLabel,
                    _lastRemoteLabel,
                    UseUtcTime,
                    _settings.Current.Use24HourClock));

            StatusText = passes.Count == 0
                ? _l.Get("Mutual.Status.NoPasses", FilterPredictionHours, localSite.GridSquare, remoteSite.GridSquare)
                : _l.Get("Pass.CountMutual", passes.Count, FilterPredictionHours);
        }
        catch (ArgumentException)
        {
            StatusText = _l.Get("Mutual.Status.InvalidGrid");
        }
        catch (Exception ex)
        {
            StatusText = _l.Get("Pass.FailedMutual", ex.Message);
        }
    }

    public bool CanOpenVisualizer(MutualPassRow? row) =>
        row is not null && _lastLocalSite is not null && _lastRemoteSite is not null;

    public MutualPassVisualizerViewModel? CreateVisualizerViewModel(MutualPassRow row)
    {
        if (!CanOpenVisualizer(row))
            return null;

        var vm = App.Services.GetRequiredService<MutualPassVisualizerViewModel>();
        vm.Initialize(
            row.Source,
            _lastLocalSite!,
            _lastRemoteSite!,
            UseUtcTime,
            _settings.Current.Use24HourClock,
            FilterMinElevationDeg);
        return vm;
    }

    public bool CanCopyPass(MutualPassRow? row) =>
        row is not null && _lastLocalSite is not null && _lastRemoteSite is not null;

    public string? FormatCopyText(MutualPassRow? row)
    {
        if (!CanCopyPass(row))
            return null;

        return MutualPassCopyFormatter.Format(
            row!.Source,
            _lastLocalSite!,
            _lastRemoteSite!,
            BuildCopyLabels(),
            UseUtcTime,
            PassDisplayFormat.FromSettings(_settings.Current.Use24HourClock));
    }

    private MutualPassCopyFormatter.Labels BuildCopyLabels() => new()
    {
        Title = _l.Get("Mutual.Copy.Title"),
        Between = _l.Get("Mutual.Copy.Between"),
        TimesIn = _l.Get("Mutual.Copy.TimesIn"),
        MutualWindowHeader = _l.Get("Mutual.Copy.MutualWindow"),
        MutualWindowLine = _l.Get("Mutual.Copy.MutualWindowLine"),
        YourPassHeader = _l.Get("Mutual.Copy.YourPass"),
        RemotePassHeader = _l.Get("Mutual.Copy.RemotePass"),
        PassTimes = _l.Get("Mutual.Copy.PassTimes"),
        MaxElevation = _l.Get("Mutual.Copy.MaxElevation"),
        Azimuth = _l.Get("Mutual.Copy.Azimuth")
    };
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

    public static MutualPassRow From(
        MutualPassInfo pass,
        string localLabel,
        string remoteLabel,
        bool useUtc,
        bool use24HourClock)
    {
        var clockFormat = PassDisplayFormat.FromSettings(use24HourClock);
        return new()
        {
            Source = pass,
            SatelliteName = pass.SatelliteName,
            MutualWindowLine = PassDisplayFormat.FormatMutualWindowLine(
                pass.MutualStartUtc, pass.MutualEndUtc, useUtc: useUtc, clockFormat: clockFormat),
            OverlapDuration = PassDisplayFormat.FormatDurationMinutes(pass.Duration),
            LocalMaxEl = $"{pass.LocalPass.MaxElevationDeg:F1}°",
            RemoteMaxEl = $"{pass.RemotePass.MaxElevationDeg:F1}°",
            LocalPassLine = $"{localLabel}: {PassDisplayFormat.FormatPlannerAosLosLine(pass.LocalPass.AosUtc, pass.LocalPass.LosUtc, useUtc: useUtc, clockFormat: clockFormat)}",
            RemotePassLine = $"{remoteLabel}: {PassDisplayFormat.FormatPlannerAosLosLine(pass.RemotePass.AosUtc, pass.RemotePass.LosUtc, useUtc: useUtc, clockFormat: clockFormat)}"
        };
    }
}
