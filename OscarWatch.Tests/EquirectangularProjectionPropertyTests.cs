// Feature: test-coverage-expansion, Property 18: GeoToPixel Bounds
// Feature: test-coverage-expansion, Property 19: Antimeridian Detection
// Feature: test-coverage-expansion, Property 20: Longitude Normalisation Within 180 of Centre

using FsCheck.Xunit;
using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 7.5, 7.6, 7.7**
///
/// Property-based tests verifying equirectangular projection invariants:
/// pixel bounds for GeoToPixel, antimeridian crossing detection, and
/// longitude normalisation staying within 180 degrees of centre.
/// </summary>
public class EquirectangularProjectionPropertyTests
{
    /// <summary>
    /// Property 18: GeoToPixel Bounds.
    ///
    /// For any latitude in [-90, 90], longitude in [-180, 180], and positive
    /// width/height, GeoToPixel returns X in [0, width] and Y in [0, height].
    /// </summary>
    [Property]
    public bool GeoToPixel_returns_X_and_Y_within_bounds(double rawLat, double rawLon, int rawWidth, int rawHeight)
    {
        if (!IsFinite(rawLat) || !IsFinite(rawLon))
            return true; // skip non-finite inputs

        // Constrain lat to [-90, 90] and lon to [-180, 180]
        var lat = rawLat % 90.0;
        var lon = rawLon % 180.0;

        // Constrain width and height to positive values
        var width = (double)(Math.Abs(rawWidth % 10000) + 1);
        var height = (double)(Math.Abs(rawHeight % 10000) + 1);

        var (x, y) = EquirectangularProjection.GeoToPixel(lat, lon, width, height);

        return x >= 0.0 && x <= width && y >= 0.0 && y <= height;
    }

    /// <summary>
    /// Property 19: Antimeridian Detection.
    ///
    /// For any sequence of GeoCoordinate points where at least one consecutive
    /// pair has a longitude difference exceeding 180 degrees,
    /// CrossesAntimeridian returns true.
    /// </summary>
    [Property]
    public bool CrossesAntimeridian_returns_true_when_longitude_diff_exceeds_180(
        double rawLat1, double rawLon1, double rawLat2, double rawLon2)
    {
        if (!IsFinite(rawLat1) || !IsFinite(rawLon1) || !IsFinite(rawLat2) || !IsFinite(rawLon2))
            return true; // skip non-finite inputs

        // Constrain latitudes to valid range
        var lat1 = rawLat1 % 90.0;
        var lat2 = rawLat2 % 90.0;

        // Construct two longitudes guaranteed to have a difference > 180
        // Place lon1 in [-180, 0) and lon2 such that |lon2 - lon1| > 180
        var lon1 = -(Math.Abs(rawLon1 % 180.0) + 0.01); // negative, in (-180, -0.01]
        var lon2 = lon1 + 180.0 + (Math.Abs(rawLon2 % 10.0) + 0.01); // guaranteed diff > 180

        var points = new List<GeoCoordinate>
        {
            new(lat1, lon1),
            new(lat2, lon2)
        };

        return EquirectangularProjection.CrossesAntimeridian(points);
    }

    /// <summary>
    /// Property 20: Longitude Normalisation Within 180 of Centre.
    ///
    /// For any longitude and centre longitude, NormalizeLongitudeNear returns
    /// a value where |result - centre| &lt;= 180.
    /// </summary>
    [Property]
    public bool NormalizeLongitudeNear_returns_value_within_180_of_centre(double rawLon, double rawCentre)
    {
        if (!IsFinite(rawLon) || !IsFinite(rawCentre))
            return true; // skip non-finite inputs

        // Constrain to reasonable ranges to avoid infinite loops in the while loops
        var lon = rawLon % 720.0;
        var centre = rawCentre % 360.0;

        var result = EquirectangularProjection.NormalizeLongitudeNear(lon, centre);

        return Math.Abs(result - centre) <= 180.0;
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
