using OscarWatch.Core.Models;

namespace OscarWatch.Core.Orbit;

public interface IIlluminationPredictor
{
    Task<IReadOnlyList<IlluminationSegment>> PredictAsync(
        SatelliteCatalogEntry satellite,
        DateTime utcStart,
        TimeSpan duration,
        CancellationToken cancellationToken = default);
}
