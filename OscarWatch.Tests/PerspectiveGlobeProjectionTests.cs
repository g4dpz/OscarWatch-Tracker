using OscarWatch.Core.Geo;

namespace OscarWatch.Tests;

public sealed class PerspectiveGlobeProjectionTests
{
    private const double GeostationaryKm = 35_786.0;

    [Theory]
    [InlineData(400.0)]
    [InlineData(800.0)]
    [InlineData(GeostationaryKm)]
    public void Subpoint_projects_to_the_origin(double altitudeKm)
    {
        var camera = new GlobeCamera(51.5, -0.12, altitudeKm);

        var point = PerspectiveGlobeProjection.Project(camera, 51.5, -0.12);

        Assert.True(point.Visible);
        Assert.Equal(0.0, point.X, precision: 12);
        Assert.Equal(0.0, point.Y, precision: 12);
    }

    [Theory]
    [InlineData(400.0)]
    [InlineData(800.0)]
    [InlineData(GeostationaryKm)]
    public void Limb_radius_matches_the_closed_form(double altitudeKm)
    {
        var p = (6371.0 + altitudeKm) / 6371.0;
        var expected = Math.Sqrt((p - 1.0) / (p + 1.0));

        Assert.Equal(expected, PerspectiveGlobeProjection.LimbRadius(altitudeKm), precision: 12);
    }

    [Fact]
    public void Limb_radius_has_the_expected_size_for_leo_and_geostationary()
    {
        // Hand-checked values: about 0.17 Earth radii at 400 km and 0.86 at geostationary height.
        Assert.InRange(PerspectiveGlobeProjection.LimbRadius(400.0), 0.17, 0.18);
        Assert.InRange(PerspectiveGlobeProjection.LimbRadius(GeostationaryKm), 0.85, 0.87);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-10.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Limb_radius_is_zero_for_unusable_altitude(double altitudeKm) =>
        Assert.Equal(0.0, PerspectiveGlobeProjection.LimbRadius(altitudeKm));

    [Theory]
    [InlineData(400.0, 0.0)]    // due north
    [InlineData(400.0, 90.0)]   // due east
    [InlineData(400.0, 180.0)]  // due south
    [InlineData(400.0, 270.0)]  // due west
    [InlineData(GeostationaryKm, 45.0)]
    public void Point_just_inside_the_horizon_lies_on_the_limb(double altitudeKm, double bearingDeg)
    {
        var camera = new GlobeCamera(10.0, 20.0, altitudeKm);
        var horizonDeg = FootprintGeometry.HorizonRadiusDeg(altitudeKm);

        var (lat, lon) = Destination(camera.SubpointLatitudeDeg, camera.SubpointLongitudeDeg, horizonDeg - 1e-6, bearingDeg);
        var point = PerspectiveGlobeProjection.Project(camera, lat, lon);

        Assert.True(point.Visible);
        var radius = Math.Sqrt(point.X * point.X + point.Y * point.Y);
        Assert.Equal(PerspectiveGlobeProjection.LimbRadius(altitudeKm), radius, precision: 5);
    }

    [Theory]
    [InlineData(400.0, 0.0)]
    [InlineData(400.0, 135.0)]
    [InlineData(GeostationaryKm, 300.0)]
    public void Point_just_beyond_the_horizon_is_not_visible(double altitudeKm, double bearingDeg)
    {
        var camera = new GlobeCamera(-30.0, 100.0, altitudeKm);
        var horizonDeg = FootprintGeometry.HorizonRadiusDeg(altitudeKm);

        var (lat, lon) = Destination(camera.SubpointLatitudeDeg, camera.SubpointLongitudeDeg, horizonDeg + 0.01, bearingDeg);

        Assert.False(PerspectiveGlobeProjection.Project(camera, lat, lon).Visible);
    }

    [Fact]
    public void North_is_up_and_east_is_right_at_the_centre()
    {
        var camera = new GlobeCamera(0.0, 0.0, 800.0);

        var north = PerspectiveGlobeProjection.Project(camera, 5.0, 0.0);
        var east = PerspectiveGlobeProjection.Project(camera, 0.0, 5.0);

        Assert.True(north.Y > 0 && Math.Abs(north.X) < 1e-12, $"north was ({north.X}, {north.Y})");
        Assert.True(east.X > 0 && Math.Abs(east.Y) < 1e-12, $"east was ({east.X}, {east.Y})");
    }

    [Fact]
    public void Pole_inside_the_footprint_projects_straight_up()
    {
        // 10 degrees from the pole, with a horizon radius of about 20 degrees at 800 km.
        var camera = new GlobeCamera(80.0, 35.0, 800.0);

        var pole = PerspectiveGlobeProjection.Project(camera, 90.0, 0.0);

        Assert.True(pole.Visible);
        Assert.True(pole.Y > 0);
        Assert.Equal(0.0, pole.X, precision: 9);
    }

    [Fact]
    public void Pole_beyond_the_footprint_is_not_visible()
    {
        var camera = new GlobeCamera(0.0, 0.0, GeostationaryKm);

        Assert.False(PerspectiveGlobeProjection.Project(camera, 90.0, 0.0).Visible);
        Assert.False(PerspectiveGlobeProjection.Project(camera, -90.0, 0.0).Visible);
    }

    [Fact]
    public void Points_either_side_of_the_antimeridian_land_on_opposite_sides_of_the_centre()
    {
        var camera = new GlobeCamera(0.0, 179.9, 800.0);

        var east = PerspectiveGlobeProjection.Project(camera, 0.0, -179.9); // 0.2 degrees east of the subpoint
        var west = PerspectiveGlobeProjection.Project(camera, 0.0, 179.7);  // 0.2 degrees west of the subpoint

        Assert.True(east.Visible && west.Visible);
        Assert.True(east.X > 0, $"east.X was {east.X}");
        Assert.True(west.X < 0, $"west.X was {west.X}");
        Assert.Equal(-west.X, east.X, precision: 9);
    }

    [Fact]
    public void Longitude_given_with_a_full_turn_offset_projects_identically()
    {
        var camera = new GlobeCamera(20.0, -170.0, 800.0);

        var a = PerspectiveGlobeProjection.Project(camera, 22.0, -168.0);
        var b = PerspectiveGlobeProjection.Project(camera, 22.0, 192.0);

        Assert.Equal(a.X, b.X, precision: 9);
        Assert.Equal(a.Y, b.Y, precision: 9);
    }

    [Fact]
    public void Unproject_of_the_origin_returns_the_subpoint()
    {
        var camera = new GlobeCamera(-33.9, 151.2, 600.0);

        Assert.True(PerspectiveGlobeProjection.TryUnproject(camera, 0.0, 0.0, out var lat, out var lon));

        Assert.Equal(-33.9, lat, precision: 9);
        Assert.Equal(151.2, lon, precision: 9);
    }

    [Fact]
    public void Unproject_beyond_the_limb_misses_the_globe()
    {
        var camera = new GlobeCamera(0.0, 0.0, 800.0);
        var beyond = PerspectiveGlobeProjection.LimbRadius(800.0) * 1.01;

        Assert.False(PerspectiveGlobeProjection.TryUnproject(camera, beyond, 0.0, out _, out _));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    [InlineData(double.NaN)]
    public void Unusable_camera_reports_everything_as_a_miss(double altitudeKm)
    {
        var camera = new GlobeCamera(10.0, 10.0, altitudeKm);

        Assert.False(PerspectiveGlobeProjection.Project(camera, 10.0, 10.0).Visible);
        Assert.False(PerspectiveGlobeProjection.TryUnproject(camera, 0.0, 0.0, out _, out _));
    }

    [Fact]
    public void Basis_matches_the_static_helpers()
    {
        var camera = new GlobeCamera(40.0, -75.0, 500.0);
        var basis = new GlobeBasis(camera);

        var viaStatic = PerspectiveGlobeProjection.Project(camera, 41.0, -74.0);
        var viaBasis = basis.Project(41.0, -74.0);

        Assert.Equal(viaStatic, viaBasis);
        Assert.Equal(PerspectiveGlobeProjection.LimbRadius(500.0), basis.LimbRadius, precision: 12);
    }

    [Fact]
    public void Hit_normal_is_a_unit_vector_pointing_up_at_the_centre()
    {
        var basis = new GlobeBasis(new GlobeCamera(0.0, 0.0, 800.0));

        Assert.True(basis.TryHitLocal(0.0, 0.0, out var x, out var y, out var z));

        Assert.Equal(0.0, x, precision: 12);
        Assert.Equal(0.0, y, precision: 12);
        Assert.Equal(1.0, z, precision: 12);
    }

    /// <summary>Great-circle destination from a start point, a central angle and a bearing.</summary>
    internal static (double LatDeg, double LonDeg) Destination(
        double latDeg, double lonDeg, double distanceDeg, double bearingDeg)
    {
        var lat = latDeg * Math.PI / 180.0;
        var lon = lonDeg * Math.PI / 180.0;
        var d = distanceDeg * Math.PI / 180.0;
        var b = bearingDeg * Math.PI / 180.0;

        var lat2 = Math.Asin(Math.Sin(lat) * Math.Cos(d) + Math.Cos(lat) * Math.Sin(d) * Math.Cos(b));
        var lon2 = lon + Math.Atan2(
            Math.Sin(b) * Math.Sin(d) * Math.Cos(lat),
            Math.Cos(d) - Math.Sin(lat) * Math.Sin(lat2));

        return (lat2 * 180.0 / Math.PI, lon2 * 180.0 / Math.PI);
    }
}
