using CommunityToolkit.Mvvm.ComponentModel;
using OscarWatch.Core.Display;
using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Core.Services;
using OscarWatch.Localization;
using OscarWatch.Orbit;

namespace OscarWatch.ViewModels;

public partial class MutualPassVisualizerViewModel : ViewModelBase
{
    private readonly ITleService _tleService;
    private readonly IOrbitPropagator _propagator;
    private readonly ILocalizationService _l;

    private MutualPassInfo? _pass;
    private GroundStation? _westSite;
    private GroundStation? _eastSite;
    private PassInfo? _westPass;
    private PassInfo? _eastPass;
    private SatelliteCatalogEntry? _satellite;
    private bool _useUtcTime;

    public MutualPassVisualizerViewModel(
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
    private string _westFullPassLabel = "";

    [ObservableProperty]
    private string _eastFullPassLabel = "";

    [ObservableProperty]
    private string _westPlotHeader = "";

    [ObservableProperty]
    private string _eastPlotHeader = "";

    [ObservableProperty]
    private PassPolarPlotData? _westPlotData;

    [ObservableProperty]
    private PassPolarPlotData? _eastPlotData;

    [ObservableProperty]
    private bool _showFullWestPass;

    [ObservableProperty]
    private bool _showFullEastPass;

    [ObservableProperty]
    private double _minimumElevationDeg;

    public void Initialize(
        MutualPassInfo pass,
        GroundStation localSite,
        GroundStation remoteSite,
        bool useUtcTime,
        bool use24HourClock,
        double minimumElevationDeg)
    {
        _pass = pass;
        _useUtcTime = useUtcTime;
        var clockFormat = PassDisplayFormat.FromSettings(use24HourClock);
        MinimumElevationDeg = minimumElevationDeg;
        (_westSite, _eastSite, _westPass, _eastPass) = OrderWestEast(localSite, remoteSite, pass);

        _satellite = _tleService.Catalog.FirstOrDefault(s => s.NoradId == pass.NoradId);
        if (_satellite is not null)
            _propagator.LoadSatellite(_satellite);

        var westLabel = StationLabel(_westSite!);
        var eastLabel = StationLabel(_eastSite!);

        HeadingText = _l.Get(
            "Mutual.Visualizer.Heading",
            westLabel,
            eastLabel,
            pass.SatelliteName);

        SubtitleText = _l.Get(
            "Mutual.Visualizer.Subtitle",
            PassDisplayFormat.FormatOverlapDurationPrecise(pass.Duration),
            PassDisplayFormat.FormatMutualOverlapStart(pass.MutualStartUtc, useUtcTime, clockFormat),
            PassDisplayFormat.FormatTimeZoneLabel(useUtcTime));

        WestFullPassLabel = _l.Get("Mutual.Visualizer.FullPassLocal", westLabel);
        EastFullPassLabel = _l.Get("Mutual.Visualizer.FullPassRemote", eastLabel);

        RebuildPlots();
    }

    partial void OnShowFullWestPassChanged(bool value) => RebuildPlots();

    partial void OnShowFullEastPassChanged(bool value) => RebuildPlots();

    private void RebuildPlots()
    {
        if (_pass is null || _westSite is null || _eastSite is null || _westPass is null || _eastPass is null || _satellite is null)
        {
            WestPlotData = null;
            EastPlotData = null;
            WestPlotHeader = "";
            EastPlotHeader = "";
            return;
        }

        WestPlotData = PassPolarPlotBuilder.Build(
            _satellite,
            _propagator,
            _westSite,
            _westPass,
            ShowFullWestPass,
            _pass.MutualStartUtc,
            _pass.MutualEndUtc,
            MinimumElevationDeg);

        EastPlotData = PassPolarPlotBuilder.Build(
            _satellite,
            _propagator,
            _eastSite,
            _eastPass,
            ShowFullEastPass,
            _pass.MutualStartUtc,
            _pass.MutualEndUtc,
            MinimumElevationDeg);

        WestPlotHeader = FormatStationHeader(WestPlotData);
        EastPlotHeader = FormatStationHeader(EastPlotData);
    }

    private static (GroundStation West, GroundStation East, PassInfo WestPass, PassInfo EastPass) OrderWestEast(
        GroundStation localSite,
        GroundStation remoteSite,
        MutualPassInfo pass)
    {
        if (localSite.LongitudeDeg <= remoteSite.LongitudeDeg)
            return (localSite, remoteSite, pass.LocalPass, pass.RemotePass);

        return (remoteSite, localSite, pass.RemotePass, pass.LocalPass);
    }

    private string FormatStationHeader(PassPolarPlotData? data)
    {
        if (data is null)
            return "";

        return _l.Get(
            "Mutual.Visualizer.StationStats",
            data.StationLabel,
            data.AosAzimuthDeg,
            data.MaxElevationDeg,
            data.LosAzimuthDeg);
    }

    private static string StationLabel(GroundStation site) =>
        string.IsNullOrWhiteSpace(site.GridSquare)
            ? site.DisplayName
            : site.GridSquare.ToUpperInvariant();
}
