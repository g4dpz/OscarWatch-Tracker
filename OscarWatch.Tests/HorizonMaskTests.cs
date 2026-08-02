using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

public sealed class HorizonMaskTests
{
    [Fact]
    public void ElevationAt_empty_returns_zero()
    {
        var mask = new HorizonMask();
        Assert.Equal(0, mask.ElevationAt(90));
        Assert.Equal(5, mask.EffectiveFloor(90, 5));
    }

    [Fact]
    public void ElevationAt_single_point_is_constant()
    {
        var mask = new HorizonMask
        {
            Points = [new HorizonMaskPoint(45, 20)]
        };

        Assert.Equal(20, mask.ElevationAt(0));
        Assert.Equal(20, mask.ElevationAt(45));
        Assert.Equal(20, mask.ElevationAt(180));
        Assert.Equal(20, mask.EffectiveFloor(10, 5));
        Assert.Equal(25, mask.EffectiveFloor(10, 25));
    }

    [Fact]
    public void ElevationAt_interpolates_between_points()
    {
        var mask = new HorizonMask
        {
            Points =
            [
                new HorizonMaskPoint(0, 10),
                new HorizonMaskPoint(180, 30)
            ]
        };

        Assert.Equal(10, mask.ElevationAt(0), 3);
        Assert.Equal(30, mask.ElevationAt(180), 3);
        Assert.Equal(20, mask.ElevationAt(90), 3);
    }

    [Fact]
    public void ElevationAt_wraps_across_north()
    {
        var mask = new HorizonMask
        {
            Points =
            [
                new HorizonMaskPoint(350, 10),
                new HorizonMaskPoint(10, 30)
            ]
        };

        // Midway through wrap: 350 → 10 spans 20°; at 0° is 10° along that span → 20°.
        Assert.Equal(20, mask.ElevationAt(0), 3);
        Assert.Equal(10, mask.ElevationAt(350), 3);
        Assert.Equal(30, mask.ElevationAt(10), 3);
    }

    [Fact]
    public void Normalize_clamps_and_merges_duplicate_azimuths()
    {
        var mask = new HorizonMask
        {
            Points =
            [
                new HorizonMaskPoint(90, 100),
                new HorizonMaskPoint(90, 15),
                new HorizonMaskPoint(-10, -5),
                new HorizonMaskPoint(370, 12)
            ]
        };

        mask.Normalize();

        // -10 → 350 el 0; 370 → 10 el 12; 90 twice → last wins el 15.
        Assert.Equal(3, mask.Points.Count);
        Assert.Contains(mask.Points, p => Math.Abs(p.AzimuthDeg - 10) < 1e-6 && Math.Abs(p.ElevationDeg - 12) < 1e-6);
        Assert.Contains(mask.Points, p => Math.Abs(p.AzimuthDeg - 90) < 1e-6 && Math.Abs(p.ElevationDeg - 15) < 1e-6);
        Assert.Contains(mask.Points, p => Math.Abs(p.AzimuthDeg - 350) < 1e-6 && Math.Abs(p.ElevationDeg) < 1e-6);
    }

    [Fact]
    public void StationProfile_round_trips_mask_via_GroundStation()
    {
        var profile = new StationProfile
        {
            DisplayName = "Hill",
            HorizonMask = new HorizonMask
            {
                Points = [new HorizonMaskPoint(0, 5), new HorizonMaskPoint(90, 25)]
            }
        };

        var site = profile.ToGroundStation();
        Assert.Equal(2, site.HorizonMask.Points.Count);
        Assert.Equal(25, site.HorizonMask.ElevationAt(90), 3);

        site.HorizonMask.Points.Add(new HorizonMaskPoint(180, 40));
        var back = StationProfile.FromGroundStation(site, profile.Id);
        Assert.Equal(3, back.HorizonMask.Points.Count);
        Assert.Equal(profile.Id, back.Id);
        // Clone: mutating site after ToGroundStation should not affect original profile until FromGroundStation.
        Assert.Equal(2, profile.HorizonMask.Points.Count);
    }
}
