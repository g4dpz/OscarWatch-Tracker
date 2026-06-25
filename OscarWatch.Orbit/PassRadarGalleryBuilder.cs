using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;

namespace OscarWatch.Orbit;

public static class PassRadarGalleryBuilder
{
    public static IReadOnlyList<PassPolarPlotData> BuildPlots(
        SatelliteCatalogEntry satellite,
        IOrbitPropagator propagator,
        GroundStation site,
        IEnumerable<PassInfo> passes,
        double minimumElevationDeg)
    {
        var plots = new List<PassPolarPlotData>();
        foreach (var pass in passes.OrderBy(p => p.AosUtc))
        {
            plots.Add(PassPolarPlotBuilder.Build(
                satellite,
                propagator,
                site,
                pass,
                useFullPass: true,
                pass.AosUtc,
                pass.LosUtc,
                minimumElevationDeg,
                includeMutualMarkers: false));
        }

        return plots;
    }
}
