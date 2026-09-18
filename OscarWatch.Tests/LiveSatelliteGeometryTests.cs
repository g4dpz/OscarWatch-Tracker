using OscarWatch.Core.Models;
using OscarWatch.Orbit;

namespace OscarWatch.Tests;

public class LiveSatelliteGeometryTests
{
    private static SatelliteCatalogEntry IssEntry => new()
    {
        Name = "ISS (ZARYA)",
        NoradId = "25544",
        Line1 = "1 25544U 98067A   25205.51782528  .00016717  00000+0  10270-3 0  9993",
        Line2 = "2 25544  51.6416 247.4627 0006703 130.5360 325.0288 15.50415322908603"
    };

    private static GroundStation LondonSite => new()
    {
        LatitudeDeg = 51.5,
        LongitudeDeg = -0.1,
        AltitudeMetersAsl = 50
    };

    [Fact]
    public void GetLiveGeometry_matches_separate_look_subpoint_and_eci()
    {
        var propagator = new PublicOrbitToolsPropagator();
        propagator.LoadSatellite(IssEntry);
        var utc = new DateTime(2025, 7, 24, 12, 0, 0, DateTimeKind.Utc);

        var look = propagator.GetLookAngles(IssEntry.NoradId, LondonSite, utc);
        var subpoint = propagator.GetSubpoint(IssEntry.NoradId, utc);
        var eci = propagator.GetEciPosition(IssEntry.NoradId, utc);
        var combined = propagator.GetLiveGeometry(IssEntry.NoradId, LondonSite, utc);

        Assert.NotNull(combined.LookAngles);
        Assert.Equal(look.AzimuthDeg, combined.LookAngles!.AzimuthDeg, 6);
        Assert.Equal(look.ElevationDeg, combined.LookAngles.ElevationDeg, 6);
        Assert.Equal(look.RangeKm, combined.LookAngles.RangeKm, 6);
        Assert.Equal(look.RangeRateKmPerSec, combined.LookAngles.RangeRateKmPerSec, 6);
        Assert.Equal(subpoint.LatitudeDeg, combined.Subpoint.LatitudeDeg, 6);
        Assert.Equal(subpoint.LongitudeDeg, combined.Subpoint.LongitudeDeg, 6);
        Assert.Equal(subpoint.AltitudeKm, combined.Subpoint.AltitudeKm, 6);
        Assert.Equal(eci.XKm, combined.Eci.XKm, 6);
        Assert.Equal(eci.YKm, combined.Eci.YKm, 6);
        Assert.Equal(eci.ZKm, combined.Eci.ZKm, 6);
        Assert.Null(combined.LookAnglesError);
    }

    [Fact]
    public void GetLiveGeometry_evaluates_PositionEci_once()
    {
        var propagator = new PublicOrbitToolsPropagator();
        propagator.LoadSatellite(IssEntry);
        var utc = new DateTime(2025, 7, 24, 12, 0, 0, DateTimeKind.Utc);

        propagator.Clear();
        propagator.LoadSatellite(IssEntry);
        Assert.Equal(0, propagator.SatellitePositionEciCount);

        _ = propagator.GetLiveGeometry(IssEntry.NoradId, LondonSite, utc);
        Assert.Equal(1, propagator.SatellitePositionEciCount);

        _ = propagator.GetLookAngles(IssEntry.NoradId, LondonSite, utc);
        _ = propagator.GetSubpoint(IssEntry.NoradId, utc);
        _ = propagator.GetEciPosition(IssEntry.NoradId, utc);
        Assert.Equal(4, propagator.SatellitePositionEciCount);
    }

    [Fact]
    public void GetLiveGeometry_skips_range_rate_when_not_requested()
    {
        var propagator = new PublicOrbitToolsPropagator();
        propagator.LoadSatellite(IssEntry);
        var utc = new DateTime(2025, 7, 24, 12, 0, 0, DateTimeKind.Utc);

        var withRate = propagator.GetLiveGeometry(IssEntry.NoradId, LondonSite, utc, includeRangeRate: true);
        var rateCountAfterInclude = propagator.RangeRateComputationCount;
        Assert.Equal(1, rateCountAfterInclude);
        Assert.NotNull(withRate.LookAngles);

        var withoutRate = propagator.GetLiveGeometry(IssEntry.NoradId, LondonSite, utc, includeRangeRate: false);
        Assert.Equal(rateCountAfterInclude, propagator.RangeRateComputationCount);
        Assert.NotNull(withoutRate.LookAngles);
        Assert.Equal(0, withoutRate.LookAngles!.RangeRateKmPerSec);
        Assert.Equal(withRate.LookAngles!.AzimuthDeg, withoutRate.LookAngles.AzimuthDeg, 6);
        Assert.Equal(withRate.LookAngles.ElevationDeg, withoutRate.LookAngles.ElevationDeg, 6);
        Assert.Equal(withRate.LookAngles.RangeKm, withoutRate.LookAngles.RangeKm, 6);
    }

    [Fact]
    public void GetLookAngles_still_computes_range_rate()
    {
        var propagator = new PublicOrbitToolsPropagator();
        propagator.LoadSatellite(IssEntry);
        var utc = new DateTime(2025, 7, 24, 12, 0, 0, DateTimeKind.Utc);

        _ = propagator.GetLiveGeometry(IssEntry.NoradId, LondonSite, utc, includeRangeRate: false);
        Assert.Equal(0, propagator.RangeRateComputationCount);

        var look = propagator.GetLookAngles(IssEntry.NoradId, LondonSite, utc);
        Assert.Equal(1, propagator.RangeRateComputationCount);
        // Near TCA rate can be small; just ensure the path ran (count) and az/el are finite.
        Assert.True(double.IsFinite(look.RangeRateKmPerSec));
        Assert.True(double.IsFinite(look.AzimuthDeg));
    }
}
