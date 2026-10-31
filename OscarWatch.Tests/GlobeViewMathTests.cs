using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

public sealed class GlobeViewMathTests
{
    [Theory]
    [InlineData(400.0, 0.0)]
    [InlineData(400.0, 5.0)]
    [InlineData(35_786.0, 0.0)]
    public void Footprint_radius_matches_the_footprint_geometry(double altitudeKm, double minimumElevationDeg) =>
        Assert.Equal(
            FootprintGeometry.HorizonRadiusDeg(altitudeKm, minimumElevationDeg),
            GlobeViewMath.FootprintRadiusDeg(altitudeKm, minimumElevationDeg));

    [Fact]
    public void Higher_minimum_elevation_gives_a_smaller_footprint()
    {
        var horizon = GlobeViewMath.FootprintRadiusDeg(800.0, 0.0);
        var usable = GlobeViewMath.FootprintRadiusDeg(800.0, 10.0);

        Assert.True(usable > 0 && usable < horizon, $"horizon {horizon}, usable {usable}");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    public void Footprint_radius_is_zero_for_unusable_altitude(double altitudeKm) =>
        Assert.Equal(0.0, GlobeViewMath.FootprintRadiusDeg(altitudeKm, 0.0));

    [Theory]
    [InlineData(0.0, 0.0, 12.0)]
    [InlineData(51.5, -0.1, 20.0)]
    [InlineData(-45.0, 170.0, 5.0)]   // ring crosses the antimeridian
    [InlineData(80.0, 35.0, 25.0)]    // ring encloses the pole
    public void Every_ring_point_lies_at_the_stated_distance_from_the_subpoint(
        double lat, double lon, double radiusDeg)
    {
        var subpoint = new GeoCoordinate(lat, lon);

        var ring = GlobeViewMath.FootprintRing(subpoint, radiusDeg);

        Assert.Equal(180, ring.Count);
        foreach (var p in ring)
        {
            var distance = GlobeViewMath.CentralAngleDeg(lat, lon, p.LatitudeDeg, p.LongitudeDeg);
            Assert.Equal(radiusDeg, distance, precision: 6);
            Assert.InRange(p.LongitudeDeg, -180.0, 180.0);
        }
    }

    [Fact]
    public void Ring_points_respect_the_requested_count()
    {
        Assert.Equal(36, GlobeViewMath.FootprintRing(new GeoCoordinate(0, 0), 10.0, points: 36).Count);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-3.0)]
    [InlineData(double.NaN)]
    public void Ring_is_empty_for_an_unusable_radius(double radiusDeg) =>
        Assert.Empty(GlobeViewMath.FootprintRing(new GeoCoordinate(0, 0), radiusDeg));

    [Fact]
    public void Station_inside_the_footprint_is_detected()
    {
        var subpoint = new GeoCoordinate(51.0, 0.0);

        Assert.True(GlobeViewMath.IsInsideFootprint(subpoint, 52.0, 1.0, radiusDeg: 10.0));
        Assert.True(GlobeViewMath.IsInsideFootprint(subpoint, 51.0, 0.0, radiusDeg: 0.001));
    }

    [Fact]
    public void Station_outside_the_footprint_is_rejected()
    {
        var subpoint = new GeoCoordinate(51.0, 0.0);

        Assert.False(GlobeViewMath.IsInsideFootprint(subpoint, 40.0, 40.0, radiusDeg: 10.0));
        Assert.False(GlobeViewMath.IsInsideFootprint(subpoint, -51.0, 180.0, radiusDeg: 10.0));
    }

    [Fact]
    public void Footprint_test_works_near_a_pole_where_longitudes_converge()
    {
        var subpoint = new GeoCoordinate(88.0, 0.0);

        // 2 degrees from the pole on the far side is 4 degrees from the subpoint over the pole.
        Assert.True(GlobeViewMath.IsInsideFootprint(subpoint, 88.0, 180.0, radiusDeg: 5.0));
        Assert.False(GlobeViewMath.IsInsideFootprint(subpoint, 80.0, 180.0, radiusDeg: 5.0));
    }

    [Theory]
    [InlineData(0.0, double.NaN)]
    [InlineData(double.NaN, 0.0)]
    public void Station_with_unusable_coordinates_is_outside(double lat, double lon) =>
        Assert.False(GlobeViewMath.IsInsideFootprint(new GeoCoordinate(0, 0), lat, lon, radiusDeg: 90.0));

    [Fact]
    public void Track_entirely_in_view_is_one_run()
    {
        var camera = new GlobeCamera(0.0, 0.0, 800.0);
        var track = Enumerable.Range(0, 10)
            .Select(i => new GeoCoordinate(0.0, i * 0.5))
            .ToList();

        var runs = GlobeViewMath.ProjectTrack(camera, track);

        var run = Assert.Single(runs);
        Assert.Equal(10, run.Count);
    }

    [Fact]
    public void Track_points_beyond_the_limb_are_omitted()
    {
        // Horizon radius at 800 km is about 28 degrees, so points from 40 degrees east are hidden.
        var camera = new GlobeCamera(0.0, 0.0, 800.0);
        var track = new List<GeoCoordinate>
        {
            new(0, 0), new(0, 10), new(0, 20), new(0, 40), new(0, 50)
        };

        var runs = GlobeViewMath.ProjectTrack(camera, track);

        var run = Assert.Single(runs);
        Assert.Equal(3, run.Count);
    }

    [Fact]
    public void Track_that_leaves_and_returns_is_split_into_two_runs()
    {
        var camera = new GlobeCamera(0.0, 0.0, 800.0);
        var track = new List<GeoCoordinate>
        {
            new(0, -10), new(0, 0), new(0, 10),   // in view
            new(0, 60), new(0, 90),               // hidden
            new(0, 12), new(0, 5), new(0, -5)     // back in view
        };

        var runs = GlobeViewMath.ProjectTrack(camera, track);

        Assert.Equal(2, runs.Count);
        Assert.Equal(3, runs[0].Count);
        Assert.Equal(3, runs[1].Count);
    }

    [Fact]
    public void Nan_gap_marker_splits_a_run_even_when_both_sides_are_visible()
    {
        var camera = new GlobeCamera(0.0, 0.0, 800.0);
        var track = new List<GeoCoordinate>
        {
            new(0, 0), new(0, 1), new(0, 2),
            new(double.NaN, double.NaN),
            new(0, 3), new(0, 4)
        };

        var runs = GlobeViewMath.ProjectTrack(camera, track);

        Assert.Equal(2, runs.Count);
    }

    [Fact]
    public void Isolated_visible_points_are_dropped_because_they_cannot_form_a_line()
    {
        var camera = new GlobeCamera(0.0, 0.0, 800.0);
        var track = new List<GeoCoordinate> { new(0, 0), new(0, 90), new(0, 1), new(0, 90) };

        Assert.Empty(GlobeViewMath.ProjectTrack(camera, track));
    }

    [Fact]
    public void Track_across_the_antimeridian_stays_continuous()
    {
        var camera = new GlobeCamera(0.0, 179.0, 800.0);
        var track = new List<GeoCoordinate>
        {
            new(0, 178.0), new(0, 179.0), new(0, 180.0), new(0, -179.0), new(0, -178.0)
        };

        var run = Assert.Single(GlobeViewMath.ProjectTrack(camera, track));

        Assert.Equal(5, run.Count);
        for (var i = 1; i < run.Count; i++)
            Assert.True(run[i].X > run[i - 1].X, $"x should increase eastwards at index {i}");
    }

    [Fact]
    public void Track_with_an_unusable_camera_projects_nothing()
    {
        var track = new List<GeoCoordinate> { new(0, 0), new(0, 1) };

        Assert.Empty(GlobeViewMath.ProjectTrack(new GlobeCamera(0.0, 0.0, 0.0), track));
    }

    [Fact]
    public void Short_tracks_project_nothing()
    {
        var camera = new GlobeCamera(0.0, 0.0, 800.0);

        Assert.Empty(GlobeViewMath.ProjectTrack(camera, []));
        Assert.Empty(GlobeViewMath.ProjectTrack(camera, [new GeoCoordinate(0, 0)]));
    }

    [Fact]
    public void Ring_at_the_horizon_radius_projects_onto_the_limb_circle()
    {
        var camera = new GlobeCamera(35.0, -20.0, 800.0);
        var subpoint = new GeoCoordinate(camera.SubpointLatitudeDeg, camera.SubpointLongitudeDeg, camera.AltitudeKm);
        var radiusDeg = GlobeViewMath.FootprintRadiusDeg(camera.AltitudeKm, 0.0) - 1e-6;
        var limb = PerspectiveGlobeProjection.LimbRadius(camera.AltitudeKm);

        foreach (var p in GlobeViewMath.FootprintRing(subpoint, radiusDeg))
        {
            var projected = PerspectiveGlobeProjection.Project(camera, p.LatitudeDeg, p.LongitudeDeg);
            Assert.True(projected.Visible);
            Assert.Equal(limb, Math.Sqrt(projected.X * projected.X + projected.Y * projected.Y), precision: 5);
        }
    }
}
