using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Core.Tle;

namespace OscarWatch.Core.Services;

public sealed class TrackingOrchestrator
{
    private readonly ISettingsService _settings;
    private readonly ITleService _tleService;
    private readonly IOrbitPropagator _propagator;
    private readonly IGroundGeometry _groundGeometry;
    private readonly IPassPredictor _passPredictor;
    private readonly ITrackingDiagnostics _diagnostics;
    private readonly SatelliteVisualCache _visualCache = new();
    private readonly HashSet<string> _loggedLookAngleSkips = new(StringComparer.Ordinal);
    private readonly HashSet<string> _loggedStateSkips = new(StringComparer.Ordinal);
    private IReadOnlyList<SatelliteCatalogEntry> _cachedEnabledSats = Array.Empty<SatelliteCatalogEntry>();

    public TrackingOrchestrator(
        ISettingsService settings,
        ITleService tleService,
        IOrbitPropagator propagator,
        IGroundGeometry groundGeometry,
        IPassPredictor passPredictor,
        ITrackingDiagnostics? diagnostics = null)
    {
        _settings = settings;
        _tleService = tleService;
        _propagator = propagator;
        _groundGeometry = groundGeometry;
        _passPredictor = passPredictor;
        _diagnostics = diagnostics ?? NullTrackingDiagnostics.Instance;
    }

    public void ReloadEnabledSatellites()
    {
        _propagator.Clear();
        _visualCache.Clear();
        _loggedLookAngleSkips.Clear();
        _loggedStateSkips.Clear();
        var sats = _tleService.GetEnabledSatellites(_settings.Current);
        _cachedEnabledSats = sats;
        foreach (var sat in sats)
            _propagator.LoadSatellite(sat);
    }

    /// <summary>Clears cached ground tracks and footprints (e.g. after map-time scrub).</summary>
    public void InvalidateVisualCache() => _visualCache.Clear();

    /// <summary>Propagates all enabled satellites at <paramref name="utc"/>. UI should use <see cref="ILiveTrackingService"/>.</summary>
    public IReadOnlyList<SatelliteTrackState> GetLiveStates(DateTime utc)
    {
        var site = _settings.Current.GroundStation;
        var sats = _cachedEnabledSats;
        var states = new List<SatelliteTrackState>();
        var sunEci = SunPositionCalculator.GetPosition(utc);

        foreach (var sat in sats)
        {
            if (!_propagator.HasSatellite(sat.NoradId))
                continue;

            try
            {
                LookAngles? look = null;
                try
                {
                    look = _propagator.GetLookAngles(sat.NoradId, site, utc);
                }
                catch (Exception ex)
                {
                    if (_loggedLookAngleSkips.Add(sat.NoradId))
                        _diagnostics.LookAnglesSkipped(sat.NoradId, utc, ex);
                }

                var subpoint = _propagator.GetSubpoint(sat.NoradId, utc);
                var cache = _visualCache.GetOrAdd(sat.NoradId);
                var altKm = TleAltitude.ResolveAltitudeKm(subpoint.AltitudeKm, sat);

                if (!_visualCache.TryGetFreshGroundTrack(sat.NoradId, utc, out var groundTrack))
                {
                    var periodMin = EstimatePeriodMinutes(sat);
                    var halfPeriod = TimeSpan.FromMinutes(periodMin / 2.0);
                    groundTrack = _groundGeometry.GetGroundTrack(
                        sat, utc - halfPeriod, utc + halfPeriod, TimeSpan.FromSeconds(120));
                    cache.GroundTrack = groundTrack;
                    cache.GroundTrackUtc = utc;
                }

                if (!_visualCache.TryGetFreshFootprint(sat.NoradId, utc, out var footprint))
                {
                    footprint = _groundGeometry.GetFootprint(sat, utc, minimumElevationDeg: 0);
                    cache.Footprint = footprint;
                    cache.FootprintUtc = utc;
                    cache.FootprintRadiusDeg = FootprintGeometry.HorizonRadiusDeg(altKm, minimumElevationDeg: 0);
                }
                else if (cache.FootprintRadiusDeg <= 0)
                {
                    cache.FootprintRadiusDeg = FootprintGeometry.HorizonRadiusDeg(altKm, minimumElevationDeg: 0);
                }

                var footprintRadiusDeg = cache.FootprintRadiusDeg > 0
                    ? cache.FootprintRadiusDeg
                    : FootprintGeometry.EstimateRingRadiusDeg(subpoint, footprint);

                var satEci = _propagator.GetEciPosition(sat.NoradId, utc);
                var isSunlit = SatelliteIllumination.IsSunlit(satEci, sunEci);
                states.Add(new SatelliteTrackState
                {
                    Name = sat.Name,
                    NoradId = sat.NoradId,
                    Subpoint = subpoint,
                    LookAngles = look,
                    GroundTrack = groundTrack,
                    Footprint = footprint,
                    FootprintRadiusDeg = footprintRadiusDeg,
                    IsSunlit = isSunlit
                });
            }
            catch (Exception ex)
            {
                if (_loggedStateSkips.Add(sat.NoradId))
                    _diagnostics.SatelliteStateSkipped(sat.NoradId, utc, ex);
            }
        }

        return states;
    }

    /// <summary>Compass azimuth a few seconds ahead for rotator north-wrap lookahead.</summary>
    public double? TryGetAheadAzimuthDeg(string noradId, double secondsAhead = 3.0)
    {
        if (!_propagator.HasSatellite(noradId))
            return null;

        try
        {
            var look = _propagator.GetLookAngles(
                noradId,
                _settings.Current.GroundStation,
                DateTime.UtcNow.AddSeconds(secondsAhead));
            return look.AzimuthDeg;
        }
        catch (Exception ex)
        {
            if (_loggedLookAngleSkips.Add(noradId))
                _diagnostics.LookAnglesSkipped(noradId, DateTime.UtcNow.AddSeconds(secondsAhead), ex);
            return null;
        }
    }

    public Task<IReadOnlyList<PassInfo>> GetUpcomingPassesAsync(CancellationToken cancellationToken = default)
    {
        var s = _settings.Current;
        return GetPassesAsync(
            s.GroundStation,
            s.MinimumElevationDeg,
            s.PassPredictionHours,
            s.PassFilterMinDurationMinutes,
            cancellationToken);
    }

    public async Task<IReadOnlyList<PassInfo>> GetPassesAsync(
        GroundStation site,
        double minimumElevationDeg,
        int predictionHours,
        int minimumDurationMinutes,
        CancellationToken cancellationToken = default)
    {
        var utcStart = DateTime.UtcNow;
        var utcEnd = utcStart.AddHours(predictionHours);
        var minDuration = TimeSpan.FromMinutes(Math.Max(0, minimumDurationMinutes));

        var sats = _tleService.GetEnabledSatellites(_settings.Current);
        var tasks = sats.Select(sat =>
            _passPredictor.GetPassesAsync(sat, site, utcStart, utcEnd, minimumElevationDeg, cancellationToken))
            .ToList();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch
        {
            // Allow partial results to be collected below.
        }

        return tasks
            .Where(t => t.IsCompletedSuccessfully)
            .SelectMany(t => t.Result)
            .Where(p => p.Duration >= minDuration)
            .OrderBy(p => p.AosUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<MutualPassInfo>> GetMutualPassesAsync(
        GroundStation localSite,
        GroundStation remoteSite,
        double minimumElevationDeg,
        int predictionHours,
        int minimumPassDurationMinutes,
        int minimumMutualDurationMinutes,
        CancellationToken cancellationToken = default)
    {
        var utcStart = DateTime.UtcNow;
        var utcEnd = utcStart.AddHours(predictionHours);
        var minPassDuration = TimeSpan.FromMinutes(Math.Max(0, minimumPassDurationMinutes));
        var minMutualDuration = TimeSpan.FromMinutes(Math.Max(0, minimumMutualDurationMinutes));

        var sats = _tleService.GetEnabledSatellites(_settings.Current);

        var localTasks = sats.Select(sat =>
            _passPredictor.GetPassesAsync(sat, localSite, utcStart, utcEnd, minimumElevationDeg, cancellationToken))
            .ToList();
        var remoteTasks = sats.Select(sat =>
            _passPredictor.GetPassesAsync(sat, remoteSite, utcStart, utcEnd, minimumElevationDeg, cancellationToken))
            .ToList();

        var allTasks = localTasks.Concat(remoteTasks).ToList();
        try
        {
            await Task.WhenAll(allTasks);
        }
        catch
        {
            // Allow partial results to be collected below
        }

        var localPasses = localTasks
            .Where(t => t.IsCompletedSuccessfully)
            .SelectMany(t => t.Result)
            .Where(p => p.Duration >= minPassDuration)
            .ToList();

        var remotePasses = remoteTasks
            .Where(t => t.IsCompletedSuccessfully)
            .SelectMany(t => t.Result)
            .Where(p => p.Duration >= minPassDuration)
            .ToList();

        return MutualPassFinder.FindOverlaps(localPasses, remotePasses, minMutualDuration);
    }

    private static double EstimatePeriodMinutes(SatelliteCatalogEntry sat)
    {
        if (sat.Line2.Length < 52)
            return 90;
        try
        {
            var meanMotion = double.Parse(sat.Line2.AsSpan(52, 11));
            if (meanMotion > 0)
                return 1440.0 / meanMotion;
        }
        catch
        {
            // ignore
        }
        return 90;
    }
}
