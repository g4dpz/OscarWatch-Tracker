using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OscarWatch.Core.Display;
using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Core.Services;
using OscarWatch.Localization;
using OscarWatch.Orbit;

namespace OscarWatch.ViewModels;

public sealed class PassRadarCardViewModel
{
    public required string TitleText { get; init; }
    public required string TimeText { get; init; }
    public required string StatsText { get; init; }
    public required PassPolarPlotData PlotData { get; init; }
    public required double MinimumElevationDeg { get; init; }
    public required bool UseUtcTime { get; init; }
    public required bool Use24HourClock { get; init; }
}

public partial class PassRadarGalleryViewModel : ViewModelBase
{
    private readonly ITleService _tleService;
    private readonly IOrbitPropagator _propagator;
    private readonly ILocalizationService _l;

    public PassRadarGalleryViewModel(
        ITleService tleService,
        IOrbitPropagator propagator,
        ILocalizationService localization)
    {
        _tleService = tleService;
        _propagator = propagator;
        _l = localization;
    }

    public ObservableCollection<PassRadarCardViewModel> Cards { get; } = [];

    [ObservableProperty]
    private string _headingText = "";

    [ObservableProperty]
    private string _subtitleText = "";

    [ObservableProperty]
    private double _minimumElevationDeg;

    [ObservableProperty]
    private bool _useUtcTime;

    [ObservableProperty]
    private bool _use24HourClock;

    public void Initialize(
        string satelliteName,
        GroundStation site,
        IReadOnlyList<PassInfo> passes,
        bool useUtcTime,
        bool use24HourClock,
        double minimumElevationDeg,
        int predictionHours)
    {
        UseUtcTime = useUtcTime;
        Use24HourClock = use24HourClock;
        MinimumElevationDeg = minimumElevationDeg;

        var stationLabel = string.IsNullOrWhiteSpace(site.GridSquare)
            ? site.DisplayName
            : site.GridSquare.ToUpperInvariant();

        HeadingText = _l.Get("PassRadar.Gallery.Heading", satelliteName, stationLabel);
        SubtitleText = _l.Get(
            "PassRadar.Gallery.Subtitle",
            passes.Count,
            predictionHours,
            minimumElevationDeg,
            PassDisplayFormat.FormatTimeZoneLabel(useUtcTime));

        Cards.Clear();
        if (passes.Count == 0)
            return;

        var satellite = _tleService.Catalog.FirstOrDefault(s => s.NoradId == passes[0].NoradId);
        if (satellite is null)
            return;

        _propagator.LoadSatellite(satellite);

        var plots = PassRadarGalleryBuilder.BuildPlots(
            satellite,
            _propagator,
            site,
            passes,
            minimumElevationDeg);

        var clockFormat = PassDisplayFormat.FromSettings(use24HourClock);
        var orderedPasses = passes.OrderBy(p => p.AosUtc).ToList();

        for (var i = 0; i < orderedPasses.Count; i++)
        {
            var pass = orderedPasses[i];
            var plot = plots[i];
            var timeText = PassDisplayFormat.FormatGalleryPassTime(
                pass.AosUtc,
                useUtcTime,
                clockFormat);

            Cards.Add(new PassRadarCardViewModel
            {
                TitleText = satelliteName,
                TimeText = timeText,
                StatsText = _l.Get(
                    "Mutual.Visualizer.StationStats",
                    plot.StationLabel,
                    plot.AosAzimuthDeg,
                    plot.MaxElevationDeg,
                    plot.LosAzimuthDeg),
                PlotData = plot,
                MinimumElevationDeg = minimumElevationDeg,
                UseUtcTime = useUtcTime,
                Use24HourClock = use24HourClock
            });
        }
    }
}
