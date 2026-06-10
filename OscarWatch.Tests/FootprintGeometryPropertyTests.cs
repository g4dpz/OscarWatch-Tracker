// Feature: test-coverage-expansion, Property 8: Horizon Radius Range and Monotonicity
// Feature: test-coverage-expansion, Property 9: Pole Containment Correctness

using FsCheck.Xunit;
using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 3.2, 3.3, 3.4, 3.5**
///
/// Property-based tests verifying footprint geometry invariants of
/// <see cref="FootprintGeometry"/>: horizon radius range and monotonicity,
/// and polar cap containment correctness.
/// </summary>
public class FootprintGeometryPropertyTests
{
    /// <summary>
    /// Property 8: Horizon Radius Range and Monotonicity.
    ///
    /// For any two positive altitudes a &lt; b, HorizonRadiusDeg(a) is in (0, 90)
    /// and HorizonRadiusDeg(a) &lt; HorizonRadiusDeg(b) (strictly monotonically increasing).
    /// </summary>
    [Property]
    public bool Horizon_radius_is_in_range_and_monotonically_increasing(double rawA, double rawB)
    {
        if (!IsFinite(rawA) || !IsFinite(rawB))
            return true; // skip non-finite inputs

        // Constrain to positive altitudes using modular arithmetic, then ensure a < b
        var a = Math.Abs(rawA % 50000.0) + 0.001; // positive altitude in km
        var b = Math.Abs(rawB % 50000.0) + 0.001;

        if (Math.Abs(a - b) < 0.001)
            b = a + 1.0; // ensure distinct values

        var low = Math.Min(a, b);
        var high = Math.Max(a, b);

        var radiusLow = FootprintGeometry.HorizonRadiusDeg(low);
        var radiusHigh = FootprintGeometry.HorizonRadiusDeg(high);

        // Both must be in (0, 90)
        var inRange = radiusLow > 0.0 && radiusLow < 90.0
                   && radiusHigh > 0.0 && radiusHigh < 90.0;

        // Monotonically increasing
        var monotonic = radiusLow < radiusHigh;

        return inRange && monotonic;
    }

    /// <summary>
    /// Property 9: Pole Containment Correctness (North Pole).
    ///
    /// For any subpoint latitude and footprint radius, ContainsNorthPole returns
    /// true if and only if lat + radius >= 90.
    /// </summary>
    [Property]
    public bool ContainsNorthPole_returns_true_iff_lat_plus_radius_gte_90(double rawLat, double rawRadius)
    {
        if (!IsFinite(rawLat) || !IsFinite(rawRadius))
            return true; // skip non-finite inputs

        var lat = rawLat % 90.0;
        var radius = Math.Abs(rawRadius % 180.0);

        var subpoint = new GeoCoordinate(lat, 0);
        var result = FootprintGeometry.ContainsNorthPole(subpoint, radius);
        var expected = lat + radius >= 90.0;

        return result == expected;
    }

    /// <summary>
    /// Property 9: Pole Containment Correctness (South Pole).
    ///
    /// For any subpoint latitude and footprint radius, ContainsSouthPole returns
    /// true if and only if lat - radius &lt;= -90.
    /// </summary>
    [Property]
    public bool ContainsSouthPole_returns_true_iff_lat_minus_radius_lte_neg90(double rawLat, double rawRadius)
    {
        if (!IsFinite(rawLat) || !IsFinite(rawRadius))
            return true; // skip non-finite inputs

        var lat = rawLat % 90.0;
        var radius = Math.Abs(rawRadius % 180.0);

        var subpoint = new GeoCoordinate(lat, 0);
        var result = FootprintGeometry.ContainsSouthPole(subpoint, radius);
        var expected = lat - radius <= -90.0;

        return result == expected;
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
