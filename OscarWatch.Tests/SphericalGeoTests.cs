using OscarWatch.Core.Geo;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 2.7**
///
/// Example-based tests verifying <see cref="SphericalGeo"/> against known
/// reference values.
/// </summary>
public sealed class SphericalGeoTests
{
    [Fact]
    public void London_to_new_york_angular_distance_matches_expected()
    {
        // London: 51.5074°N, 0.1278°W
        const double londonLat = 51.5074;
        const double londonLon = -0.1278;

        // New York: 40.7128°N, 74.0060°W
        const double newYorkLat = 40.7128;
        const double newYorkLon = -74.0060;

        // Expected great-circle angular distance ≈ 50.09 degrees
        const double expectedDeg = 50.0942;

        var actual = SphericalGeo.AngularDistanceDeg(londonLat, londonLon, newYorkLat, newYorkLon);

        Assert.Equal(expectedDeg, actual, precision: 2); // within 0.01 degrees
    }
}
