using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Orbit;

namespace OscarWatch.Tests;

/// <summary>
/// Test fixture providing a synthetic TLE and pre-computed zenith pass data for
/// PassProfileBuilder tests, RotatorController keyhole tests, and integration tests.
/// The TLE uses ISS-like orbital parameters (inclination ~51.6°, mean motion ~15.5 rev/day,
/// ~400 km altitude) to produce a near-zenith pass over the default ground station at 51.5°N, 0.1°W.
/// </summary>
internal static class ZenithPassFixture
{
    /// <summary>
    /// Default ground station: 51.5°N, 0.1°W, 50 m altitude.
    /// </summary>
    public static readonly GroundStation DefaultStation = new()
    {
        DisplayName = "ZenithTest",
        LatitudeDeg = 51.5,
        LongitudeDeg = -0.1,
        AltitudeMetersAsl = 50
    };

    /// <summary>
    /// Synthetic ISS-like TLE with inclination 51.6400°, mean motion 15.50 rev/day (~400 km altitude),
    /// near-circular orbit (eccentricity 0.0006703). Epoch: 2024-001 (1 Jan 2024 12:00:00 UTC).
    /// NORAD ID 25544 (ISS), used as a realistic test satellite.
    /// </summary>
    public static readonly SatelliteCatalogEntry ZenithSatellite = new()
    {
        Name = "ZENITH-TEST",
        NoradId = "25544",
        Line1 = "1 25544U 98067A   24001.50000000  .00016717  00000-0  10270-3 0  9993",
        Line2 = "2 25544  51.6400 247.4627 0006703 130.5360 325.0288 15.49519779439320"
    };

    /// <summary>
    /// TLE epoch: 1 January 2024 12:00:00 UTC (day 001.50000000).
    /// </summary>
    public static readonly DateTime TleEpochUtc = new(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Minimum elevation (degrees) for a pass to qualify as a "zenith pass" in this fixture.
    /// </summary>
    public const double MinZenithElevationDeg = 85.0;

    /// <summary>
    /// Scans forward from the TLE epoch to find the first pass with max elevation ≥ <paramref name="minElevationDeg"/>
    /// over the default station. Returns the AOS/TCA/LOS window and maximum elevation.
    /// </summary>
    /// <param name="propagator">Orbit propagator (must have <see cref="ZenithSatellite"/> loaded).</param>
    /// <param name="minElevationDeg">Minimum max elevation to qualify (default 85°).</param>
    /// <param name="searchDays">Maximum number of days to scan forward from epoch (default 5).</param>
    /// <returns>
    /// A tuple of (AOS, TCA, LOS, MaxElevationDeg) or <c>null</c> if no qualifying pass is found.
    /// </returns>
    public static ZenithPassResult? FindZenithPass(
        IOrbitPropagator propagator,
        double minElevationDeg = MinZenithElevationDeg,
        int searchDays = 5)
    {
        var noradId = ZenithSatellite.NoradId;
        var scanStart = TleEpochUtc;
        var scanEnd = scanStart.AddDays(searchDays);

        // Phase 1: coarse scan at 30-second intervals to find candidate pass windows
        var inPass = false;
        var passAos = DateTime.MinValue;
        var bestEl = 0.0;
        var bestTca = DateTime.MinValue;

        for (var t = scanStart; t < scanEnd; t = t.AddSeconds(30))
        {
            var look = propagator.GetLookAngles(noradId, DefaultStation, t);

            if (look.ElevationDeg > 0)
            {
                if (!inPass)
                {
                    inPass = true;
                    passAos = t;
                    bestEl = look.ElevationDeg;
                    bestTca = t;
                }
                else if (look.ElevationDeg > bestEl)
                {
                    bestEl = look.ElevationDeg;
                    bestTca = t;
                }
            }
            else if (inPass)
            {
                // Pass ended — check if it qualifies
                if (bestEl >= minElevationDeg)
                {
                    // Phase 2: refine AOS, TCA, LOS at 1-second resolution
                    return RefinePass(propagator, noradId, passAos.AddSeconds(-30), t, bestTca);
                }

                inPass = false;
                bestEl = 0.0;
            }
        }

        return null;
    }

    /// <summary>
    /// Creates a loaded <see cref="PublicOrbitToolsPropagator"/> with the <see cref="ZenithSatellite"/> TLE.
    /// </summary>
    public static PublicOrbitToolsPropagator CreatePropagator()
    {
        var propagator = new PublicOrbitToolsPropagator();
        propagator.LoadSatellite(ZenithSatellite);
        return propagator;
    }

    /// <summary>
    /// Computes the RAAN (Right Ascension of the Ascending Node) needed for a ground track
    /// to pass over the target longitude at a given epoch. This is useful for constructing
    /// custom TLEs that produce predictable zenith passes.
    /// </summary>
    /// <param name="targetLongitudeDeg">Target longitude (negative for west).</param>
    /// <param name="epochUtc">TLE epoch time.</param>
    /// <returns>RAAN in degrees [0, 360).</returns>
    /// <remarks>
    /// For a satellite at inclination matching station latitude, a near-zenith pass occurs
    /// when the ascending node is approximately 90° west of the station longitude,
    /// adjusted for Earth's rotation between the node crossing and the latitude crossing.
    /// This is an approximation; the actual pass geometry depends on many factors.
    /// </remarks>
    public static double ComputeRaanForGroundTrack(double targetLongitudeDeg, DateTime epochUtc)
    {
        // Earth rotation rate: ~360.9856°/day (sidereal)
        const double earthRotationDegPerDay = 360.9856;

        // For an orbit at ~51.6° inclination, the satellite crosses the target latitude
        // roughly 1/4 orbit (~23 minutes) after the ascending node.
        // In that time, Earth rotates by: 23 min * (360.9856° / 1440 min) ≈ 5.77°
        const double quarterOrbitEarthRotationDeg = 5.77;

        // GMST at J2000 epoch (approximation)
        var j2000 = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var daysSinceJ2000 = (epochUtc - j2000).TotalDays;
        var gmst = (280.46061837 + earthRotationDegPerDay * daysSinceJ2000) % 360.0;

        // RAAN ≈ target longitude + GMST - quarter-orbit Earth rotation
        var raan = (targetLongitudeDeg + gmst - quarterOrbitEarthRotationDeg) % 360.0;
        if (raan < 0) raan += 360.0;

        return raan;
    }

    private static ZenithPassResult RefinePass(
        IOrbitPropagator propagator,
        string noradId,
        DateTime searchStart,
        DateTime searchEnd,
        DateTime coarseTca)
    {
        // Find precise AOS (first second with elevation > 0)
        var aos = searchStart;
        for (var t = searchStart; t < searchEnd; t = t.AddSeconds(1))
        {
            var look = propagator.GetLookAngles(noradId, DefaultStation, t);
            if (look.ElevationDeg > 0)
            {
                aos = t;
                break;
            }
        }

        // Find precise LOS (last second with elevation > 0)
        var los = searchEnd;
        for (var t = searchEnd; t > aos; t = t.AddSeconds(-1))
        {
            var look = propagator.GetLookAngles(noradId, DefaultStation, t);
            if (look.ElevationDeg > 0)
            {
                los = t;
                break;
            }
        }

        // Find precise TCA (maximum elevation) within the pass at 1-second resolution
        var tca = aos;
        var maxEl = 0.0;
        for (var t = aos; t <= los; t = t.AddSeconds(1))
        {
            var look = propagator.GetLookAngles(noradId, DefaultStation, t);
            if (look.ElevationDeg > maxEl)
            {
                maxEl = look.ElevationDeg;
                tca = t;
            }
        }

        return new ZenithPassResult(aos, tca, los, maxEl);
    }
}

/// <summary>
/// Result of a zenith pass search: AOS, TCA, LOS times and the maximum elevation achieved.
/// </summary>
internal sealed record ZenithPassResult(
    DateTime AosUtc,
    DateTime TcaUtc,
    DateTime LosUtc,
    double MaxElevationDeg)
{
    /// <summary>
    /// Creates a <see cref="PassInfo"/> from this zenith pass result.
    /// </summary>
    public PassInfo ToPassInfo(double? aosAzimuthDeg = null, double? losAzimuthDeg = null) => new()
    {
        SatelliteName = ZenithPassFixture.ZenithSatellite.Name,
        NoradId = ZenithPassFixture.ZenithSatellite.NoradId,
        AosUtc = AosUtc,
        LosUtc = LosUtc,
        MaxElevationDeg = MaxElevationDeg,
        MaxElevationUtc = TcaUtc,
        AosAzimuthDeg = aosAzimuthDeg ?? 0,
        LosAzimuthDeg = losAzimuthDeg ?? 0
    };
}
