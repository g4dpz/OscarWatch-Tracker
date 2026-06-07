// Feature: performance-optimisations, Property 4: Site cache does not alter look-angle results

using FsCheck.Xunit;
using OscarWatch.Core.Models;
using OscarWatch.Orbit;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 4.5**
///
/// Property-based tests verifying that the <see cref="PublicOrbitToolsPropagator"/>
/// Site cache does not alter look-angle results. Two successive calls with the same
/// inputs must return identical AzimuthDeg, ElevationDeg, and RangeKm values.
/// </summary>
public class SiteCachePropertyTests
{
    private static readonly SatelliteCatalogEntry IssSatellite = new()
    {
        Name = "ISS (ZARYA)",
        NoradId = "25544",
        Line1 = "1 25544U 98067A   26141.16510469  .00005835  00000-0  11282-3 0  9994",
        Line2 = "2 25544  51.6328  73.8715 0007529  81.3651 278.8190 15.49291753567565"
    };

    private static readonly DateTime FixedUtc = new(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Property 4: Site cache does not alter look-angle results.
    ///
    /// Generates arbitrary (lat, lon, alt) tuples within valid ranges and asserts that
    /// calling GetLookAngles twice in succession with the same inputs returns identical results.
    /// This proves the cache doesn't corrupt results.
    /// </summary>
    [Property]
    public bool Site_cache_does_not_alter_look_angle_results(int latInt, int lonInt, int altInt)
    {
        // Map integers to valid ranges to avoid NaN/Infinity issues with raw doubles
        var lat = (latInt % 9000) / 100.0;   // [-90, 90] degrees
        var lon = (lonInt % 18000) / 100.0;  // [-180, 180] degrees
        var alt = Math.Abs(altInt % 1000) / 1000.0; // [0, 1] km

        var propagator = new PublicOrbitToolsPropagator();
        propagator.LoadSatellite(IssSatellite);

        var groundStation = new GroundStation
        {
            LatitudeDeg = lat,
            LongitudeDeg = lon,
            AltitudeMetersAsl = alt * 1000.0 // Convert km to meters
        };

        var first = propagator.GetLookAngles(IssSatellite.NoradId, groundStation, FixedUtc);
        var second = propagator.GetLookAngles(IssSatellite.NoradId, groundStation, FixedUtc);

        return first.AzimuthDeg == second.AzimuthDeg
            && first.ElevationDeg == second.ElevationDeg
            && first.RangeKm == second.RangeKm;
    }
}
