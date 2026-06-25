using OscarWatch.Core.Models;
using OscarWatch.Orbit;

namespace OscarWatch.Tests;

public class PassRadarGalleryBuilderTests
{
    private static readonly SatelliteCatalogEntry IssEntry = new()
    {
        Name = "ISS (ZARYA)",
        NoradId = "25544",
        Line1 = "1 25544U 98067A   25205.51782528  .00016717  00000+0  10270-3 0  9993",
        Line2 = "2 25544  51.6416 247.4627 0006703 130.5360 325.0288 15.50415322908603"
    };

    private static readonly GroundStation London = new()
    {
        DisplayName = "London",
        LatitudeDeg = 51.5,
        LongitudeDeg = -0.1,
        AltitudeMetersAsl = 50,
        GridSquare = "IO91"
    };

    [Fact]
    public async Task BuildPlots_returns_one_plot_per_pass_in_aos_order()
    {
        var predictor = new BruteForcePassPredictor();
        var utcStart = new DateTime(2026, 5, 23, 0, 0, 0, DateTimeKind.Utc);
        var passes = await predictor.GetPassesAsync(
            IssEntry,
            London,
            utcStart,
            utcStart.AddDays(2),
            minimumElevationDeg: 5);

        Assert.True(passes.Count >= 2);

        var propagator = new PublicOrbitToolsPropagator();
        propagator.LoadSatellite(IssEntry);

        var plots = PassRadarGalleryBuilder.BuildPlots(
            IssEntry,
            propagator,
            London,
            passes.Take(3),
            minimumElevationDeg: 5);

        Assert.Equal(3, plots.Count);
        Assert.All(plots, p => Assert.NotEmpty(p.Segments));
        Assert.All(plots, p => Assert.Null(p.MutualStart));
        Assert.All(plots, p => Assert.Null(p.MutualEnd));
    }
}
