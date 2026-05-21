using OscarWatch.Core.Models;

namespace OscarWatch.Core.Orbit;

public interface IGroundGeometry
{
    IReadOnlyList<GeoCoordinate> GetFootprint(
        SatelliteCatalogEntry satellite,
        DateTime utc,
        double minimumElevationDeg);

    IReadOnlyList<GeoCoordinate> GetGroundTrack(
        SatelliteCatalogEntry satellite,
        DateTime utcStart,
        DateTime utcEnd,
        TimeSpan step);
}
