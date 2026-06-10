using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 3.1, 3.6, 3.7**
///
/// Edge-case tests verifying <see cref="FootprintGeometry"/> handles boundary
/// conditions correctly: zero/negative altitude, insufficient ring points, and
/// zero/negative map dimensions.
/// </summary>
public sealed class FootprintGeometryTests
{
    [Fact]
    public void HorizonRadiusDeg_zero_altitude_returns_zero()
    {
        var result = FootprintGeometry.HorizonRadiusDeg(0);

        Assert.Equal(0.0, result);
    }

    [Fact]
    public void HorizonRadiusDeg_negative_altitude_returns_zero()
    {
        var result = FootprintGeometry.HorizonRadiusDeg(-100);

        Assert.Equal(0.0, result);
    }

    [Fact]
    public void ProjectRingToMap_ring_with_fewer_than_3_points_returns_empty()
    {
        var subpoint = new GeoCoordinate(0, 0);
        var emptyRing = new List<GeoCoordinate>();
        var onePointRing = new List<GeoCoordinate> { new(10, 20) };
        var twoPointRing = new List<GeoCoordinate> { new(10, 20), new(30, 40) };

        var resultEmpty = FootprintGeometry.ProjectRingToMap(subpoint, emptyRing, 10.0, 800, 600);
        var resultOne = FootprintGeometry.ProjectRingToMap(subpoint, onePointRing, 10.0, 800, 600);
        var resultTwo = FootprintGeometry.ProjectRingToMap(subpoint, twoPointRing, 10.0, 800, 600);

        Assert.Empty(resultEmpty);
        Assert.Empty(resultOne);
        Assert.Empty(resultTwo);
    }

    [Fact]
    public void ProjectRingToMap_zero_or_negative_map_dimensions_returns_empty()
    {
        var subpoint = new GeoCoordinate(0, 0);
        var ring = new List<GeoCoordinate>
        {
            new(10, 10),
            new(10, -10),
            new(-10, 0)
        };

        var resultZeroWidth = FootprintGeometry.ProjectRingToMap(subpoint, ring, 10.0, 0, 600);
        var resultZeroHeight = FootprintGeometry.ProjectRingToMap(subpoint, ring, 10.0, 800, 0);
        var resultNegativeWidth = FootprintGeometry.ProjectRingToMap(subpoint, ring, 10.0, -100, 600);
        var resultNegativeHeight = FootprintGeometry.ProjectRingToMap(subpoint, ring, 10.0, 800, -100);

        Assert.Empty(resultZeroWidth);
        Assert.Empty(resultZeroHeight);
        Assert.Empty(resultNegativeWidth);
        Assert.Empty(resultNegativeHeight);
    }
}
