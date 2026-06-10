using OscarWatch.Core.Geo;
using Xunit;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 7.1, 7.2, 7.3, 7.4**
///
/// Boundary example tests verifying that equirectangular projection maps
/// poles and extreme longitudes to the expected pixel coordinates.
/// </summary>
public sealed class EquirectangularProjectionTests
{
    private const double MapWidth = 800.0;
    private const double MapHeight = 400.0;

    /// <summary>
    /// Requirement 7.1: North Pole (lat 90) maps to Y=0.
    /// </summary>
    [Fact]
    public void GeoToPixel_north_pole_maps_to_Y_zero()
    {
        var (_, y) = EquirectangularProjection.GeoToPixel(90.0, 0.0, MapWidth, MapHeight);

        Assert.Equal(0.0, y);
    }

    /// <summary>
    /// Requirement 7.2: South Pole (lat -90) maps to Y=height.
    /// </summary>
    [Fact]
    public void GeoToPixel_south_pole_maps_to_Y_height()
    {
        var (_, y) = EquirectangularProjection.GeoToPixel(-90.0, 0.0, MapWidth, MapHeight);

        Assert.Equal(MapHeight, y);
    }

    /// <summary>
    /// Requirement 7.3: Longitude -180 maps to X=0.
    /// </summary>
    [Fact]
    public void GeoToPixel_longitude_minus_180_maps_to_X_zero()
    {
        var (x, _) = EquirectangularProjection.GeoToPixel(0.0, -180.0, MapWidth, MapHeight);

        Assert.Equal(0.0, x);
    }

    /// <summary>
    /// Requirement 7.4: Longitude 180 maps to X=width.
    /// </summary>
    [Fact]
    public void GeoToPixel_longitude_180_maps_to_X_width()
    {
        var (x, _) = EquirectangularProjection.GeoToPixel(0.0, 180.0, MapWidth, MapHeight);

        Assert.Equal(MapWidth, x);
    }
}
