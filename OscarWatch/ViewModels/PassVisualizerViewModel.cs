using CommunityToolkit.Mvvm.ComponentModel;
using OscarWatch.Core.Display;
using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Core.Rotator;
using OscarWatch.Core.Services;
using OscarWatch.Localization;
using OscarWatch.Orbit;

namespace OscarWatch.ViewModels;

public partial class PassVisualizerViewModel : ViewModelBase
{
    private readonly ITleService _tleService;
    private readonly IOrbitPropagator _propagator;
    private readonly ISettingsService _settings;
    private readonly ILocalizationService _l;

    private PassInfo? _pass;
    private GroundStation? _site;
    private SatelliteCatalogEntry? _satellite;

    public PassVisualizerViewModel(
        ITleService tleService,
        IOrbitPropagator propagator,
        ISettingsService settings,
        ILocalizationService localization)
    {
        _tleService = tleService;
        _propagator = propagator;
        _settings = settings;
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
    private HorizonMask? _horizonMask;

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
        HorizonMask = site.HorizonMask;

        HeadingText = _l.Get("Pass.Visualizer.Heading", pass.SatelliteName, StationLabel(site));

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
            SubtitleText = "";
            return;
        }

        var clockFormat = PassDisplayFormat.FromSettings(Use24HourClock);
        SubtitleText = _l.Get(
            "Pass.Visualizer.Subtitle",
            PassDisplayFormat.FormatDurationLong(_pass.Duration),
            PassDisplayFormat.FormatPlannerAosLosLine(
                _pass.AosUtc,
                _pass.LosUtc,
                useUtc: UseUtcTime,
                clockFormat: clockFormat),
            PassDisplayFormat.FormatTimeZoneLabel(UseUtcTime));

        var plotData = PassPolarPlotBuilder.Build(
            _satellite,
            _propagator,
            _site,
            _pass,
            useFullPass: true,
            _pass.AosUtc,
            _pass.LosUtc,
            MinimumElevationDeg,
            includeMutualMarkers: false);

        ApplySmart450Preview(plotData);

        PlotData = plotData;
        PlotHeader = _l.Get(
            "Mutual.Visualizer.StationStats",
            PlotData.StationLabel,
            PlotData.AosAzimuthDeg,
            PlotData.MaxElevationDeg,
            PlotData.LosAzimuthDeg);
    }

    private void ApplySmart450Preview(PassPolarPlotData plotData)
    {
        var rotator = _settings.Current.Rotator ?? new RotatorSettings();
        if (!SmartAzimuthPassPreview.TryApply(
                plotData.Samples,
                rotator.SmartAzimuth450,
                rotator.MaxAzimuthDeg))
            return;

        var smartLine = SmartAzimuthPassPreview.UsesExtendedBand(plotData.Samples)
            ? _l.Get("Pass.Visualizer.Smart450.Extended")
            : _l.Get("Pass.Visualizer.Smart450.Primary");

        SubtitleText = SubtitleText + Environment.NewLine + smartLine;
    }

    private static string StationLabel(GroundStation site) =>
        string.IsNullOrWhiteSpace(site.GridSquare)
            ? site.DisplayName
            : site.GridSquare.ToUpperInvariant();
}
