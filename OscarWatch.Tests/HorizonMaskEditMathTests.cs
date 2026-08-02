using OscarWatch.Core.Display;

namespace OscarWatch.Tests;

public sealed class HorizonMaskEditMathTests
{
    [Fact]
    public void Round_trip_az_el_near_centre_and_horizon()
    {
        const double cx = 100, cy = 100, r = 80;
        Assert.True(HorizonMaskEditMath.TryAzElToPoint(cx, cy, r, 0, 90, out var zenith));
        Assert.InRange(zenith.X, cx - 1, cx + 1);
        Assert.InRange(zenith.Y, cy - 1, cy + 1);

        Assert.True(HorizonMaskEditMath.TryAzElToPoint(cx, cy, r, 90, 0, out var east));
        Assert.True(HorizonMaskEditMath.TryPointToAzEl(cx, cy, r, east.X, east.Y, out var az, out var el));
        Assert.InRange(az, 89, 91);
        Assert.InRange(el, 0, 1);
    }

    [Fact]
    public void FindNearestPointIndex_hits_handle()
    {
        const double cx = 100, cy = 100, r = 80;
        Assert.True(HorizonMaskEditMath.TryAzElToPoint(cx, cy, r, 45, 20, out var pt));
        var points = new List<(double, double)> { (0, 10), (45, 20), (90, 5) };
        var hit = HorizonMaskEditMath.FindNearestPointIndex(points, cx, cy, r, pt.X, pt.Y, 12);
        Assert.Equal(1, hit);
    }

    [Fact]
    public void Snap_azimuth_and_elevation()
    {
        Assert.Equal(10, HorizonMaskEditMath.SnapAzimuth(10.4));
        Assert.Equal(0, HorizonMaskEditMath.SnapAzimuth(359.7));
        Assert.Equal(12.5, HorizonMaskEditMath.SnapElevation(12.4));
        Assert.Equal(90, HorizonMaskEditMath.SnapElevation(95));
    }
}
