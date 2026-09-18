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
    IReadOnlyCollection<string> LoadedNoradIds { get; }

    /// <summary>
    /// Propagates once and returns look angles (null on look failure), subpoint, and ECI.
    /// Default calls the three individual methods; real propagators should override to share SGP4.
    /// When <paramref name="includeRangeRate"/> is false, look angles still include az/el/range
    /// but <see cref="LookAngles.RangeRateKmPerSec"/> is left at 0 (Doppler only needs the focused sat).
    /// </summary>
    LiveSatelliteGeometry GetLiveGeometry(
        string noradId,
        GroundStation site,
        DateTime utc,
        bool includeRangeRate = true)
    {
        LookAngles? look = null;
        Exception? lookError = null;
        try
        {
            look = GetLookAngles(noradId, site, utc);
            if (!includeRangeRate && look is not null && look.RangeRateKmPerSec != 0)
                look = look with { RangeRateKmPerSec = 0 };
        }
        catch (Exception ex)
        {
            // Look-angle failure must not prevent subpoint / ECI (matches TrackingOrchestrator).
            lookError = ex;
        }

        var subpoint = GetSubpoint(noradId, utc);
        var eci = GetEciPosition(noradId, utc);
        return new LiveSatelliteGeometry(look, subpoint, eci, lookError);
    }
}
