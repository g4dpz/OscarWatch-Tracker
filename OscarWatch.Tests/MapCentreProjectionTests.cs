using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;
using OscarWatch.ViewModels;
using Xunit;

namespace OscarWatch.Tests;

/// <summary>
/// Map centre longitude: projection identity at C=0, recentring, polar caps, and seam edges.
/// </summary>
public sealed class MapCentreProjectionTests
{
    private const double MapWidth = 800.0;
    private const double MapHeight = 400.0;

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(-90)]
    [InlineData(170)]
    public void GeoToPixel_centre_longitude_maps_to_mid_width(double centreLon)
    {
        var (x, _) = EquirectangularProjection.GeoToPixel(0.0, centreLon, MapWidth, MapHeight, centreLon);

        Assert.Equal(MapWidth / 2.0, x, precision: 6);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(-90)]
    [InlineData(170)]
    public void GeoToPixel_seam_longitudes_map_to_viewport_edges(double centreLon)
    {
        var (leftX, _) = EquirectangularProjection.GeoToPixel(
            0.0, centreLon - 180.0, MapWidth, MapHeight, centreLon);
        var (rightX, _) = EquirectangularProjection.GeoToPixel(
            0.0, centreLon + 180.0, MapWidth, MapHeight, centreLon);

        Assert.Equal(0.0, leftX, precision: 6);
        Assert.Equal(MapWidth, rightX, precision: 6);
    }

    [Fact]
    public void GeoToPixel_zero_centre_matches_legacy_projection()
    {
        foreach (var lon in new[] { -180.0, -90.0, 0.0, 90.0, 180.0 })
        {
            var (x0, y0) = EquirectangularProjection.GeoToPixel(10.0, lon, MapWidth, MapHeight);
            var (x1, y1) = EquirectangularProjection.GeoToPixel(10.0, lon, MapWidth, MapHeight, 0);

            Assert.Equal(x0, x1);
            Assert.Equal(y0, y1);
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(90, MapWidth / 4.0)]
    [InlineData(-90, MapWidth * 0.75)]
    [InlineData(180, MapWidth / 2.0)]
    [InlineData(-180, MapWidth / 2.0)]
    public void BasemapScrollOffsetPx_matches_centre_fraction(double centreLon, double expectedOffset)
    {
        var offset = EquirectangularProjection.BasemapScrollOffsetPx(centreLon, MapWidth);

        Assert.Equal(expectedOffset, offset, precision: 6);
    }

    [Fact]
    public void ResolveMapCentreLongitude_modes()
    {
        Assert.Equal(0.0, MainViewModel.ResolveMapCentreLongitude(MapCentreMode.Greenwich, 42, -1.5));
        Assert.Equal(-1.5, MainViewModel.ResolveMapCentreLongitude(MapCentreMode.Station, 42, -1.5));
        Assert.Equal(42.0, MainViewModel.ResolveMapCentreLongitude(MapCentreMode.Custom, 42, -1.5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(-90)]
    [InlineData(170)]
    public void Polar_cap_spans_full_width_for_any_centre(double centreLon)
    {
        var subpoint = new GeoCoordinate(85.0, centreLon);
        const double radiusDeg = 10.0;
        Assert.True(FootprintGeometry.ContainsNorthPole(subpoint, radiusDeg));

        var ring = BuildBearingRing(subpoint, radiusDeg, steps: 36);
        var pixels = FootprintGeometry.ProjectRingToMap(
            subpoint, ring, radiusDeg, MapWidth, MapHeight, centreLon);

        Assert.True(pixels.Count >= 3);
        var minX = pixels.Min(p => p.X);
        var maxX = pixels.Max(p => p.X);
        Assert.Equal(0.0, minX, precision: 1);
        Assert.Equal(MapWidth, maxX, precision: 1);

        // No large interior gap after sort (polar rim is continuous across the viewport).
        var boundaryXs = pixels
            .Where(p => p.Y > 1 && p.Y < MapHeight - 1)
            .Select(p => p.X)
            .OrderBy(x => x)
            .ToList();
        for (var i = 1; i < boundaryXs.Count; i++)
            Assert.True(boundaryXs[i] - boundaryXs[i - 1] < MapWidth * 0.2);
    }

    [Fact]
    public void Polar_cap_at_zero_centre_matches_legacy_bounds()
    {
        var subpoint = new GeoCoordinate(85.0, 0.0);
        const double radiusDeg = 10.0;
        var ring = BuildBearingRing(subpoint, radiusDeg, steps: 36);

        var legacy = FootprintGeometry.ProjectRingToMap(subpoint, ring, radiusDeg, MapWidth, MapHeight);
        var centred = FootprintGeometry.ProjectRingToMap(
            subpoint, ring, radiusDeg, MapWidth, MapHeight, 0);

        Assert.Equal(legacy.Count, centred.Count);
        for (var i = 0; i < legacy.Count; i++)
        {
            Assert.Equal(legacy[i].X, centred[i].X, precision: 6);
            Assert.Equal(legacy[i].Y, centred[i].Y, precision: 6);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(170)]
    public void Mid_latitude_footprint_on_seam_extends_near_viewport_edge(double centreLon)
    {
        var seamLon = centreLon + 179.0;
        var subpoint = new GeoCoordinate(20.0, seamLon);
        const double radiusDeg = 15.0;
        Assert.False(FootprintGeometry.ContainsNorthPole(subpoint, radiusDeg));
        Assert.False(FootprintGeometry.ContainsSouthPole(subpoint, radiusDeg));

        var ring = BuildBearingRing(subpoint, radiusDeg, steps: 36);
        var pixels = FootprintGeometry.ProjectRingToMap(
            subpoint, ring, radiusDeg, MapWidth, MapHeight, centreLon);

        var (sx, _) = EquirectangularProjection.GeoToPixel(
            subpoint.LatitudeDeg, subpoint.LongitudeDeg, MapWidth, MapHeight, centreLon);

        Assert.True(sx > MapWidth - 60, $"subpoint x={sx} should be near right edge");
        Assert.True(pixels.Max(p => p.X) > MapWidth - 60 || pixels.Min(p => p.X) < 60);
    }

    [Fact]
    public void Near_polar_non_enclosing_footprint_uses_geographic_ring_not_full_width_cap()
    {
        // lat + radius < 90 → not a polar cap
        var subpoint = new GeoCoordinate(70.0, 170.0);
        const double radiusDeg = 15.0;
        Assert.False(FootprintGeometry.ContainsNorthPole(subpoint, radiusDeg));

        var ring = BuildBearingRing(subpoint, radiusDeg, steps: 36);
        var pixels = FootprintGeometry.ProjectRingToMap(
            subpoint, ring, radiusDeg, MapWidth, MapHeight, centerLongitudeDeg: 0);

        var span = pixels.Max(p => p.X) - pixels.Min(p => p.X);
        Assert.True(span < MapWidth * 0.75, $"expected geographic ring, span={span}");
    }

    [Fact]
    public void Ground_track_across_geographic_antimeridian_stays_one_chain_when_seam_is_mid_map()
    {
        // Geographic ±180 is mid-map when centre is 0? No — mid-map is 0.
        // When centre is 0, ±180 is the edge. When centre is 90, geographic 0 is mid-map
        // and geographic ±180 is near x for lon -180 relative to 90.
        // Track crossing geographic ±180 with centre=90 should stay continuous (one chain).
        var points = new List<GeoCoordinate>();
        for (var lon = 170.0; lon <= 190.0; lon += 2.0)
        {
            var wrapped = lon > 180 ? lon - 360 : lon;
            points.Add(new GeoCoordinate(10.0, wrapped));
        }

        var chains = EquirectangularProjection.ProjectGroundTrackForDraw(
            points, MapWidth, MapHeight, centerLongitudeDeg: 90);

        Assert.Single(chains);
        Assert.Equal(points.Count, chains[0].Count);
    }

    private static List<GeoCoordinate> BuildBearingRing(
        GeoCoordinate subpoint,
        double radiusDeg,
        int steps)
    {
        var ring = new List<GeoCoordinate>(steps);
        for (var i = 0; i < steps; i++)
        {
            var bearing = i * (360.0 / steps);
            var (lat, lon) = SphericalGeo.DestinationPoint(
                subpoint.LatitudeDeg, subpoint.LongitudeDeg, radiusDeg, bearing);
            ring.Add(new GeoCoordinate(lat, lon));
        }

        return ring;
    }
}
