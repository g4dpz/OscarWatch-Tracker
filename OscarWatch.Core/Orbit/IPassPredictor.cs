using OscarWatch.Core.Models;

namespace OscarWatch.Core.Orbit;

public interface IPassPredictor
{
    Task<IReadOnlyList<PassInfo>> GetPassesAsync(
        SatelliteCatalogEntry satellite,
        GroundStation site,
        DateTime utcStart,
        DateTime utcEnd,
        double minimumElevationDeg,
        CancellationToken cancellationToken = default);
}
