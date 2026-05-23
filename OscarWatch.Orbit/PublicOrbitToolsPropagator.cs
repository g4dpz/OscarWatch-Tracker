using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;

namespace OscarWatch.Orbit;

public sealed class PublicOrbitToolsPropagator : IOrbitPropagator
{
    private readonly Dictionary<string, (SatelliteCatalogEntry Entry, Zeptomoby.OrbitTools.Orbit Orbit)> _satellites =
        new(StringComparer.Ordinal);

    public IReadOnlyList<string> LoadedNoradIds => _satellites.Keys.ToList();

    public void LoadSatellite(SatelliteCatalogEntry entry) =>
        _satellites[entry.NoradId] = (entry, OrbitToolsMapping.CreateOrbit(entry));

    public void RemoveSatellite(string noradId) => _satellites.Remove(noradId);

    public void Clear() => _satellites.Clear();

    public bool HasSatellite(string noradId) => _satellites.ContainsKey(noradId);

    public GeoCoordinate GetSubpoint(string noradId, DateTime utc)
    {
        var orbit = GetOrbit(noradId);
        return OrbitToolsMapping.ToGeoCoordinate(orbit.PositionEci(utc));
    }

    public EciPosition GetEciPosition(string noradId, DateTime utc)
    {
        var orbit = GetOrbit(noradId);
        return OrbitToolsMapping.ToEciPosition(orbit.PositionEci(utc));
    }

    public LookAngles GetLookAngles(string noradId, GroundStation site, DateTime utc)
    {
        var orbit = GetOrbit(noradId);
        var groundSite = OrbitToolsMapping.CreateSite(site);
        var eci = orbit.PositionEci(utc);
        var topo = groundSite.GetLookAngle(eci);

        var rangeRate = 0.0;
        try
        {
            var eciNext = orbit.PositionEci(utc.AddSeconds(1));
            var topoNext = groundSite.GetLookAngle(eciNext);
            rangeRate = topoNext.Range - topo.Range;
        }
        catch
        {
            // ignore decay at boundary
        }

        return OrbitToolsMapping.ToLookAngles(topo, rangeRate);
    }

    private Zeptomoby.OrbitTools.Orbit GetOrbit(string noradId)
    {
        if (!_satellites.TryGetValue(noradId, out var pair))
            throw new KeyNotFoundException($"Satellite {noradId} not loaded.");
        return pair.Orbit;
    }
}
