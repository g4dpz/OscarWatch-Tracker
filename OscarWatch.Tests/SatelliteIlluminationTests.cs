using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;

namespace OscarWatch.Tests;

public class SatelliteIlluminationTests
{
    [Fact]
    public void Day_side_position_is_sunlit()
    {
        var sun = new EciPosition(149_597_870, 0, 0);
        var satellite = new EciPosition(7000, 0, 0);

        Assert.True(SatelliteIllumination.IsSunlit(satellite, sun));
    }

    [Fact]
    public void Night_side_inside_shadow_cylinder_is_eclipse()
    {
        var sun = new EciPosition(149_597_870, 0, 0);
        var satellite = new EciPosition(-7000, 1000, 0);

        Assert.True(SatelliteIllumination.IsInEclipse(satellite, sun));
    }

    [Fact]
    public void Night_side_outside_shadow_cylinder_is_still_sunlit()
    {
        var sun = new EciPosition(149_597_870, 0, 0);
        var satellite = new EciPosition(-7000, 8000, 0);

        Assert.True(SatelliteIllumination.IsSunlit(satellite, sun));
    }

    [Fact]
    public void Sun_position_is_far_from_earth()
    {
        var sun = SunPositionCalculator.GetPosition(new DateTime(2026, 5, 23, 12, 0, 0, DateTimeKind.Utc));
        var distanceKm = Math.Sqrt(sun.XKm * sun.XKm + sun.YKm * sun.YKm + sun.ZKm * sun.ZKm);

        Assert.InRange(distanceKm, 147_000_000, 152_000_000);
    }
}
