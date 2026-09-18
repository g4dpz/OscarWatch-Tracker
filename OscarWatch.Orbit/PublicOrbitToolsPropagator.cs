using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;

namespace OscarWatch.Orbit;

public sealed class PublicOrbitToolsPropagator : IOrbitPropagator
{
    private readonly object _gate = new();
    private readonly Dictionary<string, (SatelliteCatalogEntry Entry, Zeptomoby.OrbitTools.Orbit Orbit)> _satellites =
        new(StringComparer.Ordinal);

    private readonly Dictionary<(double, double, double), Zeptomoby.OrbitTools.Site> _siteCache = new();

    /// <summary>Test hook: number of satellite <c>PositionEci</c> evaluations.</summary>
    internal long SatellitePositionEciCount { get; private set; }

    /// <summary>Test hook: number of observer/satellite range-rate evaluations.</summary>
    internal long RangeRateComputationCount { get; private set; }

    public IReadOnlyCollection<string> LoadedNoradIds
    {
        get
        {
            lock (_gate)
                return _satellites.Keys.ToArray();
        }
    }

    public void LoadSatellite(SatelliteCatalogEntry entry)
    {
        lock (_gate)
            _satellites[entry.NoradId] = (entry, OrbitToolsMapping.CreateOrbit(entry));
    }

    public void RemoveSatellite(string noradId)
    {
        lock (_gate)
            _satellites.Remove(noradId);
    }

    public void Clear()
    {
        lock (_gate)
        {
            _satellites.Clear();
            _siteCache.Clear();
            SatellitePositionEciCount = 0;
            RangeRateComputationCount = 0;
        }
    }

    public bool HasSatellite(string noradId)
    {
        lock (_gate)
            return _satellites.ContainsKey(noradId);
    }

    public GeoCoordinate GetSubpoint(string noradId, DateTime utc)
    {
        lock (_gate)
        {
            var orbit = GetOrbitUnlocked(noradId);
            SatellitePositionEciCount++;
            return OrbitToolsMapping.ToGeoCoordinate(orbit.PositionEci(utc));
        }
    }

    public EciPosition GetEciPosition(string noradId, DateTime utc)
    {
        lock (_gate)
        {
            var orbit = GetOrbitUnlocked(noradId);
            SatellitePositionEciCount++;
            return OrbitToolsMapping.ToEciPosition(orbit.PositionEci(utc));
        }
    }

    public LookAngles GetLookAngles(string noradId, GroundStation site, DateTime utc)
    {
        lock (_gate)
        {
            var orbit = GetOrbitUnlocked(noradId);
            var groundSite = GetOrCreateSiteUnlocked(site);
            SatellitePositionEciCount++;
            var satEci = orbit.PositionEci(utc);
            var topo = groundSite.GetLookAngle(satEci);
            var rangeRate = ComputeRangeRateKmPerSec(groundSite, satEci, utc);
            return OrbitToolsMapping.ToLookAngles(topo, rangeRate);
        }
    }

    /// <inheritdoc />
    public LiveSatelliteGeometry GetLiveGeometry(
        string noradId,
        GroundStation site,
        DateTime utc,
        bool includeRangeRate = true)
    {
        lock (_gate)
        {
            var orbit = GetOrbitUnlocked(noradId);
            // One SGP4 evaluation shared by look angles, subpoint, and ECI.
            SatellitePositionEciCount++;
            var satEci = orbit.PositionEci(utc);
            var subpoint = OrbitToolsMapping.ToGeoCoordinate(satEci);
            var eci = OrbitToolsMapping.ToEciPosition(satEci);

            LookAngles? look = null;
            Exception? lookError = null;
            try
            {
                var groundSite = GetOrCreateSiteUnlocked(site);
                var topo = groundSite.GetLookAngle(satEci);
                var rangeRate = includeRangeRate
                    ? ComputeRangeRateKmPerSec(groundSite, satEci, utc)
                    : 0;
                look = OrbitToolsMapping.ToLookAngles(topo, rangeRate);
            }
            catch (Exception ex)
            {
                // Preserve TrackingOrchestrator behaviour: look stays null; subpoint/ECI remain.
                lookError = ex;
            }

            return new LiveSatelliteGeometry(look, subpoint, eci, lookError);
        }
    }

    private Zeptomoby.OrbitTools.Site GetOrCreateSiteUnlocked(GroundStation site)
    {
        var key = (
            Math.Round(site.LatitudeDeg, 6),
            Math.Round(site.LongitudeDeg, 6),
            Math.Round(site.AltitudeKm, 6));

        if (!_siteCache.TryGetValue(key, out var groundSite))
        {
            groundSite = OrbitToolsMapping.CreateSite(site);
            _siteCache[key] = groundSite;
        }

        return groundSite;
    }

    private double ComputeRangeRateKmPerSec(
        Zeptomoby.OrbitTools.Site groundSite,
        Zeptomoby.OrbitTools.EciTime satEci,
        DateTime utc)
    {
        try
        {
            RangeRateComputationCount++;
            var obsEci = groundSite.PositionEci(utc);
            return RangeRateCalculator.ComputeKmPerSec(satEci, obsEci);
        }
        catch
        {
            return 0;
        }
    }

    private Zeptomoby.OrbitTools.Orbit GetOrbitUnlocked(string noradId)
    {
        if (!_satellites.TryGetValue(noradId, out var pair))
            throw new KeyNotFoundException($"Satellite {noradId} not loaded.");
        return pair.Orbit;
    }
}
