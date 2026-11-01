using OscarWatch.Controls;
using OscarWatch.Core.Geo;

namespace OscarWatch.Tests;

public sealed class GlobeRasterPolicyTests
{
    private static readonly DateTime T = new(2026, 6, 21, 12, 0, 20, DateTimeKind.Utc);

    private static RasterKey Key() => new(
        new GlobeCamera(10, 20, 420), SizePx: 512, Zoom: 1.0,
        UtcMinute: GlobeRasterPolicy.UtcMinute(T), DarkTheme: false, MapAvailable: true);

    [Theory]
    [InlineData(500.0, 1.0, 512)]
    [InlineData(512.0, 1.0, 512)]
    [InlineData(520.0, 1.0, 512)]    // rounds to the nearest step
    [InlineData(530.0, 1.0, 544)]
    [InlineData(300.0, 2.0, 608)]    // a 2x display needs twice the pixels
    [InlineData(10.0, 1.0, 64)]      // never below the minimum
    [InlineData(5000.0, 1.0, 768)]   // never above the maximum
    [InlineData(400.0, 2.0, 768)]    // 800 wanted, capped
    public void Raster_size_is_stepped_and_limited(double side, double scaling, int expected) =>
        Assert.Equal(expected, GlobeRasterPolicy.RasterSize(side, scaling));

    [Theory]
    [InlineData(0.0)]
    [InlineData(-10.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void An_unusable_side_gives_the_minimum_size(double side) =>
        Assert.Equal(GlobeRasterPolicy.MinRasterPx, GlobeRasterPolicy.RasterSize(side, 1.0));

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    public void An_unusable_scaling_is_treated_as_one(double scaling) =>
        Assert.Equal(GlobeRasterPolicy.RasterSize(500, 1.0), GlobeRasterPolicy.RasterSize(500, scaling));

    [Fact]
    public void Raster_size_is_always_a_multiple_of_the_step_inside_the_limits()
    {
        for (var side = 1.0; side < 3000; side += 7.3)
        {
            var size = GlobeRasterPolicy.RasterSize(side, 1.25);
            Assert.InRange(size, GlobeRasterPolicy.MinRasterPx, GlobeRasterPolicy.MaxRasterPx);
            Assert.Equal(0, size % GlobeRasterPolicy.SizeStepPx);
        }
    }

    [Fact]
    public void Raster_size_never_shrinks_as_the_control_grows()
    {
        var previous = 0;
        for (var side = 1.0; side < 3000; side += 3.1)
        {
            var size = GlobeRasterPolicy.RasterSize(side, 1.0);
            Assert.True(size >= previous, $"shrank at {side}");
            previous = size;
        }
    }

    [Fact]
    public void The_first_raster_is_always_needed() =>
        Assert.True(GlobeRasterPolicy.NeedsRender(null, Key()));

    [Fact]
    public void An_identical_key_does_not_need_a_new_raster() =>
        Assert.False(GlobeRasterPolicy.NeedsRender(Key(), Key()));

    [Fact]
    public void Small_movement_does_not_need_a_new_raster_but_larger_movement_does()
    {
        var k = Key();

        Assert.False(GlobeRasterPolicy.NeedsRender(k, k with { Camera = k.Camera with { SubpointLatitudeDeg = 10.04 } }));
        Assert.True(GlobeRasterPolicy.NeedsRender(k, k with { Camera = k.Camera with { SubpointLatitudeDeg = 10.06 } }));
        Assert.True(GlobeRasterPolicy.NeedsRender(k, k with { Camera = k.Camera with { SubpointLongitudeDeg = 20.2 } }));
    }

    [Fact]
    public void Movement_is_measured_as_arc_so_longitude_matters_less_near_the_poles()
    {
        // 0.2 degrees of longitude at 89 degrees north is only about 0.003 degrees of arc.
        var k = Key() with { Camera = new GlobeCamera(89, 0, 420) };

        Assert.False(GlobeRasterPolicy.NeedsRender(k, k with { Camera = k.Camera with { SubpointLongitudeDeg = 0.2 } }));
    }

    [Fact]
    public void A_tiny_step_across_the_antimeridian_is_a_tiny_move()
    {
        var east = Key() with { Camera = new GlobeCamera(0, 179.99, 420) };
        var west = east with { Camera = new GlobeCamera(0, -179.99, 420) };

        Assert.False(GlobeRasterPolicy.NeedsRender(east, west));
    }

    [Fact]
    public void Altitude_matters_only_beyond_the_tolerance()
    {
        var k = Key();

        Assert.False(GlobeRasterPolicy.NeedsRender(k, k with { Camera = k.Camera with { AltitudeKm = 420.9 } }));
        Assert.True(GlobeRasterPolicy.NeedsRender(k, k with { Camera = k.Camera with { AltitudeKm = 421.1 } }));
    }

    [Fact]
    public void Size_zoom_theme_map_and_minute_changes_each_need_a_new_raster()
    {
        var k = Key();

        Assert.True(GlobeRasterPolicy.NeedsRender(k, k with { SizePx = 544 }));
        Assert.True(GlobeRasterPolicy.NeedsRender(k, k with { Zoom = 1.5 }));
        Assert.True(GlobeRasterPolicy.NeedsRender(k, k with { DarkTheme = true }));
        Assert.True(GlobeRasterPolicy.NeedsRender(k, k with { MapAvailable = false }));
        Assert.True(GlobeRasterPolicy.NeedsRender(k, k with { UtcMinute = k.UtcMinute + 1 }));
    }

    [Fact]
    public void Seconds_within_a_minute_share_a_minute_number()
    {
        Assert.Equal(GlobeRasterPolicy.UtcMinute(T), GlobeRasterPolicy.UtcMinute(T.AddSeconds(30)));
        Assert.NotEqual(GlobeRasterPolicy.UtcMinute(T), GlobeRasterPolicy.UtcMinute(T.AddSeconds(45)));
    }

    [Fact]
    public void A_geostationary_satellite_drifting_slightly_does_not_need_a_new_raster()
    {
        var k = Key() with { Camera = new GlobeCamera(0.0, 25.5, 35_786) };

        Assert.False(GlobeRasterPolicy.NeedsRender(k, k with { Camera = new GlobeCamera(0.01, 25.5004, 35_786.3) }));
    }
}
