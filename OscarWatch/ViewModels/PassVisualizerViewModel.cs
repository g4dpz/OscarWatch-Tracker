using CommunityToolkit.Mvvm.ComponentModel;
using OscarWatch.Core.Display;
using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Core.Services;
using OscarWatch.Localization;
using OscarWatch.Orbit;

namespace OscarWatch.ViewModels;

public partial class PassVisualizerViewModel : ViewModelBase
{
    private readonly ITleService _tleService;
    private readonly IOrbitPropagator _propagator;
    private readonly ILocalizationService _l;

    private PassInfo? _pass;
    private GroundStation? _site;
    private SatelliteCatalogEntry? _satellite;

    public PassVisualizerViewModel(
        ITleService tleService,
        IOrbitPropagator propagator,
        ILocalizationService localization)
    {
        _tleService = tleService;
        _propagator = propagator;
        _l = localization;
    }

    [ObservableProperty]
    private string _headingText = "";

    [ObservableProperty]
    private string _subtitleText = "";

    [ObservableProperty]
    private string _plotHeader = "";

    [ObservableProperty]
    private PassPolarPlotData? _plotData;

    [ObservableProperty]
    private double _minimumElevationDeg;

    [ObservableProperty]
    private bool _useUtcTime;

    [ObservableProperty]
    private bool _use24HourClock;

    public void Initialize(
        PassInfo pass,
        GroundStation site,
        bool useUtcTime,
        bool use24HourClock,
        double minimumElevationDeg)
    {
        _pass = pass;
        _site = site;
        UseUtcTime = useUtcTime;
        Use24HourClock = use24HourClock;
        MinimumElevationDeg = minimumElevationDeg;

        var clockFormat = PassDisplayFormat.FromSettings(use24HourClock);
        var stationLabel = StationLabel(site);

        HeadingText = _l.Get("Pass.Visualizer.Heading", pass.SatelliteName, stationLabel);
        SubtitleText = _l.Get(
            "Pass.Visualizer.Subtitle",
            PassDisplayFormat.FormatDurationLong(pass.Duration),
            PassDisplayFormat.FormatPlannerAosLosLine(pass.AosUtc, pass.LosUtc, useUtc: useUtcTime, clockFormat: clockFormat),
            PassDisplayFormat.FormatTimeZoneLabel(useUtcTime));

        _satellite = _tleService.Catalog.FirstOrDefault(s => s.NoradId == pass.NoradId);
        if (_satellite is not null)
            _propagator.LoadSatellite(_satellite);

        RebuildPlot();
    }

    private void RebuildPlot()
    {
        if (_pass is null || _site is null || _satellite is null)
        {
            PlotData = null;
            PlotHeader = "";
            return;
        }

        PlotData = PassPolarPlotBuilder.Build(
            _satellite,
            _propagator,
            _site,
            _pass,
            useFullPass: true,
            _pass.AosUtc,
            _pass.LosUtc,
            MinimumElevationDeg,
            includeMutualMarkers: false);

        PlotHeader = _l.Get(
            "Mutual.Visualizer.StationStats",
            PlotData.StationLabel,
            PlotData.AosAzimuthDeg,
            PlotData.MaxElevationDeg,
            PlotData.LosAzimuthDeg);
    }

    private static string StationLabel(GroundStation site) =>
        string.IsNullOrWhiteSpace(site.GridSquare)
            ? site.DisplayName
            : site.GridSquare.ToUpperInvariant();
}
