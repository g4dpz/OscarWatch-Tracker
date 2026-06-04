using OscarWatch.Core.Models;
using OscarWatch.Orbit;

namespace OscarWatch.Tests;

public class PassPolarPlotBuilderTests
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
    public async Task Build_samples_pass_with_segments_and_mutual_markers()
    {
        var predictor = new BruteForcePassPredictor();
        var utcStart = new DateTime(2026, 5, 23, 0, 0, 0, DateTimeKind.Utc);
        var passes = await predictor.GetPassesAsync(
            IssEntry,
            London,
            utcStart,
            utcStart.AddDays(2),
            minimumElevationDeg: 5);

        var pass = passes[0];
        Assert.NotEmpty(passes);
        var propagator = new PublicOrbitToolsPropagator();
        propagator.LoadSatellite(IssEntry);

        var mutualStart = pass.AosUtc.AddMinutes(1);
        var mutualEnd = pass.LosUtc.AddMinutes(-1);

        var plot = PassPolarPlotBuilder.Build(
            IssEntry,
            propagator,
            London,
            pass,
            useFullPass: true,
            mutualStart,
            mutualEnd);

        Assert.Equal("IO91", plot.StationLabel);
        Assert.NotEmpty(plot.Segments);
        Assert.All(plot.Segments, s => Assert.NotEmpty(s.Points));
        Assert.NotNull(plot.MutualStart);
        Assert.NotNull(plot.MutualEnd);
    }

    [Fact]
    public async Task Build_mutual_window_only_has_fewer_points_than_full_pass()
    {
        var predictor = new BruteForcePassPredictor();
        var utcStart = new DateTime(2026, 5, 23, 0, 0, 0, DateTimeKind.Utc);
        var passes = await predictor.GetPassesAsync(
            IssEntry,
            London,
            utcStart,
            utcStart.AddDays(2),
            minimumElevationDeg: 5);

        var pass = passes[0];
        Assert.NotEmpty(passes);
        var propagator = new PublicOrbitToolsPropagator();
        propagator.LoadSatellite(IssEntry);

        var mutualStart = pass.AosUtc.AddMinutes(2);
        var mutualEnd = pass.LosUtc.AddMinutes(-2);

        var full = PassPolarPlotBuilder.Build(
            IssEntry, propagator, London, pass, true, mutualStart, mutualEnd);
        var windowOnly = PassPolarPlotBuilder.Build(
            IssEntry, propagator, London, pass, false, mutualStart, mutualEnd);

        var fullPoints = full.Segments.Sum(s => s.Points.Count);
        var windowPoints = windowOnly.Segments.Sum(s => s.Points.Count);
        Assert.True(windowPoints <= fullPoints);
        Assert.True(windowPoints > 0);
    }
}
