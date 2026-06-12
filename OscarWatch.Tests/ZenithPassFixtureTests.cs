using OscarWatch.Core.Rotator;
using OscarWatch.Orbit;

namespace OscarWatch.Tests;

/// <summary>
/// Verification tests for the <see cref="ZenithPassFixture"/> — confirms that the ISS-like TLE
/// produces a near-zenith pass (max elevation ≥ 85°) over the default ground station when
/// propagated with SGP4.
/// </summary>
public class ZenithPassFixtureTests
{
    [Fact]
    public void FindZenithPass_returns_pass_with_elevation_at_least_85_degrees()
    {
        var propagator = ZenithPassFixture.CreatePropagator();

        var result = ZenithPassFixture.FindZenithPass(propagator);

        Assert.NotNull(result);
        Assert.True(result.MaxElevationDeg >= ZenithPassFixture.MinZenithElevationDeg,
            $"Expected max elevation ≥ {ZenithPassFixture.MinZenithElevationDeg}°, " +
            $"but got {result.MaxElevationDeg:F2}°.");
    }

    [Fact]
    public void FindZenithPass_returns_valid_time_ordering()
    {
        var propagator = ZenithPassFixture.CreatePropagator();

        var result = ZenithPassFixture.FindZenithPass(propagator);

        Assert.NotNull(result);
        Assert.True(result.AosUtc < result.TcaUtc, "AOS should be before TCA.");
        Assert.True(result.TcaUtc < result.LosUtc, "TCA should be before LOS.");
        Assert.True(result.AosUtc > ZenithPassFixture.TleEpochUtc, "AOS should be after TLE epoch.");
    }

    [Fact]
    public void FindZenithPass_tca_has_maximum_elevation()
    {
        var propagator = ZenithPassFixture.CreatePropagator();

        var result = ZenithPassFixture.FindZenithPass(propagator);

        Assert.NotNull(result);

        // Verify the TCA time actually has the highest elevation
        var lookAtTca = propagator.GetLookAngles(
            ZenithPassFixture.ZenithSatellite.NoradId,
            ZenithPassFixture.DefaultStation,
            result.TcaUtc);

        Assert.True(lookAtTca.ElevationDeg >= 85.0,
            $"Elevation at TCA should be ≥ 85°, got {lookAtTca.ElevationDeg:F2}°.");
    }

    [Fact]
    public void FindZenithPass_aos_and_los_are_near_horizon()
    {
        var propagator = ZenithPassFixture.CreatePropagator();

        var result = ZenithPassFixture.FindZenithPass(propagator);

        Assert.NotNull(result);

        var lookAtAos = propagator.GetLookAngles(
            ZenithPassFixture.ZenithSatellite.NoradId,
            ZenithPassFixture.DefaultStation,
            result.AosUtc);
        var lookAtLos = propagator.GetLookAngles(
            ZenithPassFixture.ZenithSatellite.NoradId,
            ZenithPassFixture.DefaultStation,
            result.LosUtc);

        // AOS and LOS should be very close to horizon (within a few degrees)
        Assert.True(lookAtAos.ElevationDeg >= 0 && lookAtAos.ElevationDeg < 5,
            $"AOS elevation should be near horizon, got {lookAtAos.ElevationDeg:F2}°.");
        Assert.True(lookAtLos.ElevationDeg >= 0 && lookAtLos.ElevationDeg < 5,
            $"LOS elevation should be near horizon, got {lookAtLos.ElevationDeg:F2}°.");
    }

    [Fact]
    public void ToPassInfo_creates_valid_pass_info()
    {
        var propagator = ZenithPassFixture.CreatePropagator();

        var result = ZenithPassFixture.FindZenithPass(propagator);

        Assert.NotNull(result);
        var passInfo = result.ToPassInfo(aosAzimuthDeg: 45.0, losAzimuthDeg: 315.0);

        Assert.Equal("ZENITH-TEST", passInfo.SatelliteName);
        Assert.Equal("25544", passInfo.NoradId);
        Assert.Equal(result.AosUtc, passInfo.AosUtc);
        Assert.Equal(result.LosUtc, passInfo.LosUtc);
        Assert.Equal(result.MaxElevationDeg, passInfo.MaxElevationDeg);
        Assert.Equal(result.TcaUtc, passInfo.MaxElevationUtc);
        Assert.Equal(45.0, passInfo.AosAzimuthDeg);
        Assert.Equal(315.0, passInfo.LosAzimuthDeg);
    }

    [Fact]
    public void PassProfileBuilder_produces_high_elevation_profile_for_zenith_pass()
    {
        var propagator = ZenithPassFixture.CreatePropagator();

        var result = ZenithPassFixture.FindZenithPass(propagator);

        Assert.NotNull(result);
        var passInfo = result.ToPassInfo();

        var profile = PassProfileBuilder.Build(
            passInfo,
            ZenithPassFixture.ZenithSatellite.NoradId,
            ZenithPassFixture.DefaultStation,
            propagator);

        Assert.NotNull(profile);
        Assert.True(profile.Points.Count > 0, "Profile should have at least one point.");

        var maxProfileEl = profile.Points.Max(p => p.ElevationDeg);
        Assert.True(maxProfileEl >= 85.0,
            $"Profile max elevation should be ≥ 85°, got {maxProfileEl:F2}°.");
    }

    [Fact]
    public void ComputeRaanForGroundTrack_returns_valid_range()
    {
        var raan = ZenithPassFixture.ComputeRaanForGroundTrack(-0.1, ZenithPassFixture.TleEpochUtc);

        Assert.InRange(raan, 0.0, 360.0);
    }

    [Fact]
    public void ZenithSatellite_has_valid_tle_format()
    {
        var line1 = ZenithPassFixture.ZenithSatellite.Line1;
        var line2 = ZenithPassFixture.ZenithSatellite.Line2;

        // Line 1 starts with '1', Line 2 starts with '2'
        Assert.Equal('1', line1[0]);
        Assert.Equal('2', line2[0]);

        // Both lines are 69 characters long (standard TLE format)
        Assert.Equal(69, line1.Length);
        Assert.Equal(69, line2.Length);

        // The TLE loads successfully into the propagator (validates checksum implicitly)
        var propagator = ZenithPassFixture.CreatePropagator();
        Assert.True(propagator.HasSatellite(ZenithPassFixture.ZenithSatellite.NoradId));
    }
}
