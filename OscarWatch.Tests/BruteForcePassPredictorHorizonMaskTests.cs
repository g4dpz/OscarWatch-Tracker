using OscarWatch.Core.Models;
using OscarWatch.Orbit;

namespace OscarWatch.Tests;

public sealed class BruteForcePassPredictorHorizonMaskTests
{
    private static readonly SatelliteCatalogEntry IssEntry = new()
    {
        Name = "ISS (ZARYA)",
        NoradId = "25544",
        Line1 = "1 25544U 98067A   25205.51782528  .00016717  00000+0  10270-3 0  9993",
        Line2 = "2 25544  51.6416 247.4627 0006703 130.5360 325.0288 15.50415322908603"
    };

    private static GroundStation London(HorizonMask? mask = null) => new()
    {
        DisplayName = "London",
        LatitudeDeg = 51.5,
        LongitudeDeg = -0.1,
        AltitudeMetersAsl = 50,
        GridSquare = "IO91",
        HorizonMask = mask ?? new HorizonMask()
    };

    [Fact]
    public async Task Empty_mask_matches_scalar_minimum_elevation()
    {
        var predictor = new BruteForcePassPredictor();
        var utcStart = new DateTime(2026, 5, 23, 0, 0, 0, DateTimeKind.Utc);
        var site = London();

        var passes = await predictor.GetPassesAsync(
            IssEntry, site, utcStart, utcStart.AddDays(2), minimumElevationDeg: 5);

        Assert.NotEmpty(passes);
        Assert.All(passes, p => Assert.True(p.MaxElevationDeg >= 5));
    }

    [Fact]
    public async Task High_uniform_mask_reduces_or_shortens_passes()
    {
        var predictor = new BruteForcePassPredictor();
        var utcStart = new DateTime(2026, 5, 23, 0, 0, 0, DateTimeKind.Utc);

        var open = await predictor.GetPassesAsync(
            IssEntry, London(), utcStart, utcStart.AddDays(2), minimumElevationDeg: 5);

        var wall = new HorizonMask
        {
            Points =
            [
                new HorizonMaskPoint(0, 35),
                new HorizonMaskPoint(90, 35),
                new HorizonMaskPoint(180, 35),
                new HorizonMaskPoint(270, 35)
            ]
        };
        var blocked = await predictor.GetPassesAsync(
            IssEntry, London(wall), utcStart, utcStart.AddDays(2), minimumElevationDeg: 5);

        Assert.NotEmpty(open);
        // Tall skyline should drop low passes and/or shorten remaining ones.
        Assert.True(
            blocked.Count < open.Count
            || blocked.Sum(p => p.Duration.TotalSeconds) < open.Sum(p => p.Duration.TotalSeconds) * 0.95);
        Assert.All(blocked, p => Assert.True(p.MaxElevationDeg >= 35));
    }

    [Fact]
    public async Task Sector_mask_can_split_or_delay_aos_relative_to_open_sky()
    {
        var predictor = new BruteForcePassPredictor();
        var utcStart = new DateTime(2026, 5, 23, 0, 0, 0, DateTimeKind.Utc);

        var open = await predictor.GetPassesAsync(
            IssEntry, London(), utcStart, utcStart.AddDays(1), minimumElevationDeg: 0);

        // Wall on northern half — enough to change at least one pass timing vs open sky.
        var northWall = new HorizonMask
        {
            Points =
            [
                new HorizonMaskPoint(315, 40),
                new HorizonMaskPoint(0, 40),
                new HorizonMaskPoint(45, 40),
                new HorizonMaskPoint(90, 0),
                new HorizonMaskPoint(270, 0)
            ]
        };
        var masked = await predictor.GetPassesAsync(
            IssEntry, London(northWall), utcStart, utcStart.AddDays(1), minimumElevationDeg: 0);

        Assert.NotEmpty(open);
        var openTotal = open.Sum(p => p.Duration.TotalSeconds);
        var maskedTotal = masked.Sum(p => p.Duration.TotalSeconds);
        Assert.True(masked.Count != open.Count || Math.Abs(maskedTotal - openTotal) > 30);
    }
}
