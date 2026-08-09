using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;

namespace OscarWatch.Orbit;

public sealed class PublicOrbitToolsPropagator : IOrbitPropagator
{
    private readonly object _gate = new();
    private readonly Dictionary<string, (SatelliteCatalogEntry Entry, Zeptomoby.OrbitTools.Orbit Orbit)> _satellites =
        new(StringComparer.Ordinal);

    private readonly Dictionary<(double, double, double), Zeptomoby.OrbitTools.Site> _siteCache = new();

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
            return OrbitToolsMapping.ToGeoCoordinate(orbit.PositionEci(utc));
        }
    }

    public EciPosition GetEciPosition(string noradId, DateTime utc)
    {
        lock (_gate)
        {
            var orbit = GetOrbitUnlocked(noradId);
            return OrbitToolsMapping.ToEciPosition(orbit.PositionEci(utc));
        }
    }

    public LookAngles GetLookAngles(string noradId, GroundStation site, DateTime utc)
    {
        lock (_gate)
        {
            var orbit = GetOrbitUnlocked(noradId);
            var key = (
                Math.Round(site.LatitudeDeg, 6),
                Math.Round(site.LongitudeDeg, 6),
                Math.Round(site.AltitudeKm, 6));

            if (!_siteCache.TryGetValue(key, out var groundSite))
            {
                groundSite = OrbitToolsMapping.CreateSite(site);
                _siteCache[key] = groundSite;
            }

            var satEci = orbit.PositionEci(utc);
            var topo = groundSite.GetLookAngle(satEci);
            var rangeRate = ComputeRangeRateKmPerSec(groundSite, satEci, utc);
            return OrbitToolsMapping.ToLookAngles(topo, rangeRate);
        }
    }

    private static double ComputeRangeRateKmPerSec(
        Zeptomoby.OrbitTools.Site groundSite,
        Zeptomoby.OrbitTools.EciTime satEci,
        DateTime utc)
    {
        try
        {
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
