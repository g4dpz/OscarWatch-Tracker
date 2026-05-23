using OscarWatch.Core.Models;

namespace OscarWatch.Core.Orbit;

public interface IOrbitPropagator
{
    void LoadSatellite(SatelliteCatalogEntry entry);
    void RemoveSatellite(string noradId);
    void Clear();

    GeoCoordinate GetSubpoint(string noradId, DateTime utc);
    EciPosition GetEciPosition(string noradId, DateTime utc);
    LookAngles GetLookAngles(string noradId, GroundStation site, DateTime utc);
    bool HasSatellite(string noradId);
    IReadOnlyList<string> LoadedNoradIds { get; }
}
