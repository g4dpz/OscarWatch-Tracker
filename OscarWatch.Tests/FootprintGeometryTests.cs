using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;
using OscarWatch.Core.Tle;
using OscarWatch.Localization;

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

    /// <summary>
    /// Regression: Spanish UI culture must not inflate TLE-derived altitude or footprint radius.
    /// </summary>
    [Fact]
    public void Iss_footprint_radius_under_spanish_ui_culture_stays_in_leo_range()
    {
        using var cultureScope = TestUiCulture.Apply("es");

        var entry = new SatelliteCatalogEntry
        {
            Name = "ISS",
            NoradId = "25544",
            Line1 = new string('0', 69),
            Line2 = "2 25544  51.6400 247.4627 0006703 130.5360 325.0288 15.49519779439320"
        };

        // Same path as map tracking when subpoint altitude is below 100 km.
        var altKm = TleAltitude.ResolveAltitudeKm(50.0, entry);
        var radiusDeg = FootprintGeometry.HorizonRadiusDeg(altKm, minimumElevationDeg: 0);

        Assert.InRange(altKm, 360.0, 460.0);
        Assert.InRange(radiusDeg, 15.0, 35.0);

        Assert.True(TleOrbitalSanity.TryReadLine2Elements(entry.Line2, out _, out _, out var meanMotion));
        var periodMin = 1440.0 / meanMotion;
        Assert.InRange(periodMin, 85.0, 100.0);
    }
}
