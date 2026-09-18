using OscarWatch.Core.Models;
using OscarWatch.Orbit;
using Zeptomoby.OrbitTools;
using SatelliteOrbit = Zeptomoby.OrbitTools.Orbit;

namespace OscarWatch.Tests;

public sealed class BruteForcePassPredictorCoarseSampleTests
{
    private static readonly SatelliteCatalogEntry IssEntry = new()
    {
        Name = "ISS (ZARYA)",
        NoradId = "25544",
        Line1 = "1 25544U 98067A   25205.51782528  .00016717  00000+0  10270-3 0  9993",
        Line2 = "2 25544  51.6416 247.4627 0006703 130.5360 325.0288 15.50415322908603"
    };

    [Fact]
    public void EvaluateCoarseSample_matches_separate_visibility_and_elevation()
    {
        var orbit = new SatelliteOrbit(new Tle(IssEntry.Name, IssEntry.Line1, IssEntry.Line2));
        var site = new Site(51.5, -0.1, 0.05);
        var mask = new HorizonMask();
        const double minEl = 5.0;
        var start = new DateTime(2026, 5, 23, 0, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < 200; i++)
        {
            var t = start.AddSeconds(i * 30);
            bool expectedVisible;
            double expectedEl;
            try
            {
                var look = site.GetLookAngle(orbit.PositionEci(t));
                expectedVisible = look.ElevationDeg >= mask.EffectiveFloor(look.AzimuthDeg, minEl);
                expectedEl = look.ElevationDeg;
            }
            catch
            {
                expectedVisible = false;
                expectedEl = -90;
            }

            var (visible, el) = BruteForcePassPredictor.EvaluateCoarseSample(orbit, site, mask, minEl, t);
            Assert.Equal(expectedVisible, visible);
            Assert.Equal(expectedEl, el, 6);
        }
    }

    [Fact]
    public async Task GetPassesAsync_still_finds_iss_passes()
    {
        var predictor = new BruteForcePassPredictor();
        var utcStart = new DateTime(2026, 5, 23, 0, 0, 0, DateTimeKind.Utc);
        var ground = new GroundStation
        {
            LatitudeDeg = 51.5,
            LongitudeDeg = -0.1,
            AltitudeMetersAsl = 50
        };

        var passes = await predictor.GetPassesAsync(
            IssEntry, ground, utcStart, utcStart.AddHours(48), minimumElevationDeg: 5);

        Assert.NotEmpty(passes);
        Assert.All(passes, p =>
        {
            Assert.True(p.LosUtc > p.AosUtc);
            Assert.True(p.MaxElevationDeg >= 5);
            Assert.True(p.MaxElevationUtc >= p.AosUtc && p.MaxElevationUtc <= p.LosUtc);
        });
    }
}
