using FsCheck.Xunit;
using OscarWatch.Core.Geo;

namespace OscarWatch.Tests;

/// <summary>
/// Property-based tests for <see cref="PerspectiveGlobeProjection"/>: the inverse recovers the
/// input, and the visibility threshold is the same horizon that
/// <see cref="FootprintGeometry.HorizonRadiusDeg"/> describes.
/// </summary>
public class PerspectiveGlobePropertyTests
{
    /// <summary>
    /// Projecting a visible point and unprojecting the result returns the original point.
    /// Points within 5 percent of the horizon angle are skipped, because a ray that grazes the
    /// limb is numerically ill-conditioned by nature, and so are points within 0.1 degrees of
    /// a pole, where longitude has no meaning.
    /// </summary>
    [Property]
    public bool Unproject_of_project_returns_the_original_point(
        double rawCamLat, double rawCamLon, double rawAlt, double rawDistance, double rawBearing)
    {
        if (!AllFinite(rawCamLat, rawCamLon, rawAlt, rawDistance, rawBearing))
            return true;

        var camera = new GlobeCamera(
            Range(rawCamLat, -89.0, 89.0),
            Range(rawCamLon, -180.0, 180.0),
            Range(rawAlt, 200.0, 40_000.0));
        var horizonDeg = FootprintGeometry.HorizonRadiusDeg(camera.AltitudeKm);
        var distanceDeg = Range(rawDistance, 0.0, 0.95 * horizonDeg);
        var bearingDeg = Range(rawBearing, 0.0, 360.0);

        var (lat, lon) = PerspectiveGlobeProjectionTests.Destination(
            camera.SubpointLatitudeDeg, camera.SubpointLongitudeDeg, distanceDeg, bearingDeg);
        if (Math.Abs(lat) > 89.9)
            return true;

        var projected = PerspectiveGlobeProjection.Project(camera, lat, lon);
        if (!projected.Visible)
            return false;

        if (!PerspectiveGlobeProjection.TryUnproject(camera, projected.X, projected.Y, out var lat2, out var lon2))
            return false;

        return Math.Abs(lat2 - lat) < 1e-5 && Math.Abs(WrapDegrees(lon2 - lon)) < 1e-5;
    }

    /// <summary>
    /// A point 0.01 degrees inside the horizon angle is visible, and one 0.01 degrees outside is
    /// not, for any camera position, any altitude and any bearing.
    /// </summary>
    [Property]
    public bool Visibility_threshold_matches_the_footprint_horizon(
        double rawCamLat, double rawCamLon, double rawAlt, double rawBearing)
    {
        if (!AllFinite(rawCamLat, rawCamLon, rawAlt, rawBearing))
            return true;

        var camera = new GlobeCamera(
            Range(rawCamLat, -89.0, 89.0),
            Range(rawCamLon, -180.0, 180.0),
            Range(rawAlt, 100.0, 40_000.0));
        var horizonDeg = FootprintGeometry.HorizonRadiusDeg(camera.AltitudeKm);
        var bearingDeg = Range(rawBearing, 0.0, 360.0);

        var (insideLat, insideLon) = PerspectiveGlobeProjectionTests.Destination(
            camera.SubpointLatitudeDeg, camera.SubpointLongitudeDeg, horizonDeg - 0.01, bearingDeg);
        var (outsideLat, outsideLon) = PerspectiveGlobeProjectionTests.Destination(
            camera.SubpointLatitudeDeg, camera.SubpointLongitudeDeg, horizonDeg + 0.01, bearingDeg);

        return PerspectiveGlobeProjection.Project(camera, insideLat, insideLon).Visible
            && !PerspectiveGlobeProjection.Project(camera, outsideLat, outsideLon).Visible;
    }

    /// <summary>Every visible point projects inside the limb circle.</summary>
    [Property]
    public bool Visible_points_project_inside_the_limb(
        double rawCamLat, double rawCamLon, double rawAlt, double rawLat, double rawLon)
    {
        if (!AllFinite(rawCamLat, rawCamLon, rawAlt, rawLat, rawLon))
            return true;

        var camera = new GlobeCamera(
            Range(rawCamLat, -89.0, 89.0),
            Range(rawCamLon, -180.0, 180.0),
            Range(rawAlt, 200.0, 40_000.0));

        var point = PerspectiveGlobeProjection.Project(
            camera, Range(rawLat, -90.0, 90.0), Range(rawLon, -180.0, 180.0));
        if (!point.Visible)
            return true;

        var radius = Math.Sqrt(point.X * point.X + point.Y * point.Y);
        return radius <= PerspectiveGlobeProjection.LimbRadius(camera.AltitudeKm) + 1e-9;
    }

    private static bool AllFinite(params double[] values) => values.All(double.IsFinite);

    /// <summary>Maps any finite value into [min, max] by folding its fractional part.</summary>
    private static double Range(double raw, double min, double max)
    {
        var unit = Math.Abs(raw % 1.0);
        return min + unit * (max - min);
    }

    private static double WrapDegrees(double degrees)
    {
        var wrapped = degrees % 360.0;
        if (wrapped > 180.0)
            wrapped -= 360.0;
        if (wrapped < -180.0)
            wrapped += 360.0;
        return wrapped;
    }
}
