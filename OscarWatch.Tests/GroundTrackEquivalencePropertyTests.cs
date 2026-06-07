// Feature: performance-optimisations, Property 5: Ground-track equivalence after propagator routing

using FsCheck.Xunit;
using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Orbit;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 5.4**
///
/// Property-based tests verifying that <see cref="SampledGroundGeometry"/> produces
/// identical ground-track results whether the satellite is loaded in the propagator
/// (cached orbit path) or not (fallback path via <c>OrbitToolsMapping.CreateOrbit</c>).
/// Per-point lat/lon differences must be less than 1e-6 degrees.
/// </summary>
public class GroundTrackEquivalencePropertyTests
{
    private static readonly SatelliteCatalogEntry IssSatellite = new()
    {
        Name = "ISS (ZARYA)",
        NoradId = "25544",
        Line1 = "1 25544U 98067A   26141.16510469  .00005835  00000-0  11282-3 0  9994",
        Line2 = "2 25544  51.6328  73.8715 0007529  81.3651 278.8190 15.49291753567565"
    };

    /// <summary>
    /// A null propagator that always returns <c>HasSatellite = false</c>,
    /// forcing <see cref="SampledGroundGeometry"/> to use the fallback path
    /// via <c>OrbitToolsMapping.CreateOrbit</c>.
    /// </summary>
    private sealed class NullOrbitPropagator : IOrbitPropagator
    {
        public IReadOnlyCollection<string> LoadedNoradIds => Array.Empty<string>();
        public bool HasSatellite(string noradId) => false;
        public void LoadSatellite(SatelliteCatalogEntry entry) { }
        public void RemoveSatellite(string noradId) { }
        public void Clear() { }

        public GeoCoordinate GetSubpoint(string noradId, DateTime utc) =>
            throw new InvalidOperationException("NullOrbitPropagator should not be called for subpoint.");

        public EciPosition GetEciPosition(string noradId, DateTime utc) =>
            throw new InvalidOperationException("NullOrbitPropagator should not be called for ECI position.");

        public LookAngles GetLookAngles(string noradId, GroundStation site, DateTime utc) =>
            throw new InvalidOperationException("NullOrbitPropagator should not be called for look angles.");
    }

    /// <summary>
    /// Property 5: Ground-track equivalence after propagator routing.
    ///
    /// Generates arbitrary start time offsets (0–100 hours from a fixed epoch) and step sizes
    /// (10s–5min). Calls GetGroundTrack on two SampledGroundGeometry instances — one backed by
    /// a loaded propagator (cached orbit) and one backed by a NullOrbitPropagator (fallback path).
    /// Asserts that per-point lat/lon differences are less than 1e-6 degrees.
    /// </summary>
    [Property]
    public bool Ground_track_equivalence_after_propagator_routing(int startOffsetMinutes, int stepRaw)
    {
        // Constrain start offset to a reasonable window around the TLE epoch
        // TLE epoch is ~2026-05-21 (day 141.16510469 of 2026)
        var baseUtc = new DateTime(2026, 5, 21, 4, 0, 0, DateTimeKind.Utc);
        var offsetMinutes = Math.Abs(startOffsetMinutes % 1440); // 0–1440 minutes (within 1 day of epoch)
        var utcStart = baseUtc.AddMinutes(offsetMinutes);

        // Step size: 10s–300s (5 min)
        var stepSeconds = 10 + Math.Abs(stepRaw % 291); // 10–300 seconds
        var step = TimeSpan.FromSeconds(stepSeconds);

        // Fixed 30-minute window
        var utcEnd = utcStart.AddMinutes(30);

        // Path 1: Propagator with ISS loaded (cached orbit path)
        var loadedPropagator = new PublicOrbitToolsPropagator();
        loadedPropagator.LoadSatellite(IssSatellite);
        var cachedGeometry = new SampledGroundGeometry(loadedPropagator);

        // Path 2: NullOrbitPropagator (forces fallback via OrbitToolsMapping.CreateOrbit)
        var nullPropagator = new NullOrbitPropagator();
        var fallbackGeometry = new SampledGroundGeometry(nullPropagator);

        // Compute ground tracks
        var cachedTrack = cachedGeometry.GetGroundTrack(IssSatellite, utcStart, utcEnd, step);
        var fallbackTrack = fallbackGeometry.GetGroundTrack(IssSatellite, utcStart, utcEnd, step);

        // Same number of points
        if (cachedTrack.Count != fallbackTrack.Count)
            return false;

        // Per-point lat/lon differences < 1e-6 degrees
        const double tolerance = 1e-6;
        for (var i = 0; i < cachedTrack.Count; i++)
        {
            var latDiff = Math.Abs(cachedTrack[i].LatitudeDeg - fallbackTrack[i].LatitudeDeg);
            var lonDiff = Math.Abs(cachedTrack[i].LongitudeDeg - fallbackTrack[i].LongitudeDeg);

            if (latDiff >= tolerance || lonDiff >= tolerance)
                return false;
        }

        return true;
    }
}
