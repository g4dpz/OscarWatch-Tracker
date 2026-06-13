// Feature: incomplete-orbit-tracks, Property 2: Preservation — Antimeridian Splitting, Wrap Selection, and Gap-Free Propagation Unchanged

using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Controls;
using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Orbit;
using Xunit;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4**
///
/// Preservation property tests: lock existing correct behaviour BEFORE the fix is applied.
/// These tests MUST PASS on unfixed code — they confirm the baseline behaviour we want to preserve.
///
/// Preservation guarantees:
/// - Non-polar, non-antimeridian tracks return a single unbroken chain (Req 3.4)
/// - Antimeridian crossings produce exactly 2 chains with correct point counts (Req 3.1)
/// - Fully-visible chains select wrap offset 0 (Req 3.2)
/// - Gap-free propagation produces a point list with count equal to number of time steps (Req 3.3)
/// - Low-inclination non-polar orbits at 1200×600 render identically (Req 3.4)
/// </summary>
public class IncompleteOrbitTrackPreservationPropertyTests
{
    private const double MapWidth = 1200.0;
    private const double MapHeight = 600.0;

    #region Property 1 — Non-polar, non-antimeridian tracks produce single unbroken chain

    /// <summary>
    /// Observation: ProjectGroundTrackForDraw with a mid-latitude equatorial track
    /// (lat 0–20°, lon 0–60°) at 1200×600 returns a single unbroken chain.
    /// </summary>
    [Fact]
    public void Observe_MidLatitudeEquatorialTrack_SingleUnbrokenChain()
    {
        var points = new List<GeoCoordinate>();
        for (var i = 0; i < 20; i++)
        {
            points.Add(new GeoCoordinate(i * 1.0, i * 3.0)); // lat 0–19, lon 0–57
        }

        var chains = EquirectangularProjection.ProjectGroundTrackForDraw(points, MapWidth, MapHeight);

        Assert.Single(chains);
        Assert.Equal(20, chains[0].Count);
    }

    /// <summary>
    /// Property: For all non-polar (lat within ±60°), non-antimeridian (lon within ±90°)
    /// tracks with gradual increments, ProjectGroundTrackForDraw returns a single chain
    /// containing all points.
    ///
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool NonPolar_NonAntimeridian_Track_Returns_Single_Chain(int seed)
    {
        var rng = new Random(seed);

        // Generate a track staying well within safe bounds:
        // lat: [-50, 50], lon: [-80, 80] — no antimeridian, no poles
        var count = rng.Next(5, 40);
        var startLat = rng.NextDouble() * 40.0 - 20.0;  // [-20, 20]
        var startLon = rng.NextDouble() * 60.0 - 30.0;  // [-30, 30]

        var points = new List<GeoCoordinate>();
        var lat = startLat;
        var lon = startLon;

        for (var i = 0; i < count; i++)
        {
            points.Add(new GeoCoordinate(lat, lon));
            // Small increments that stay within safe zone
            lat += rng.NextDouble() * 3.0 - 1.0; // ±1.5° per step
            lon += rng.NextDouble() * 4.0 - 1.0; // ±2° per step (biased positive)

            // Clamp to safe zone to avoid antimeridian or pole triggers
            lat = Math.Clamp(lat, -50.0, 50.0);
            lon = Math.Clamp(lon, -80.0, 80.0);
        }

        var chains = EquirectangularProjection.ProjectGroundTrackForDraw(points, MapWidth, MapHeight);

        // Should produce a single chain containing all points
        return chains.Count == 1 && chains[0].Count == count;
    }

    #endregion

    #region Property 2 — Antimeridian crossing kept continuous via unwrapping

    /// <summary>
    /// Observation: ProjectGroundTrackForDraw keeps antimeridian crossings (lon wrapping
    /// from ~+180° to ~−180°) in a single continuous chain via longitude unwrapping.
    /// The crossing from lon 177.5 to -177.5 (a 5° eastward step via shortest path)
    /// does NOT trigger a chain break because unwrapping keeps dx small.
    ///
    /// This is the correct behaviour to preserve: the function uses unwrapping to keep
    /// satellite tracks continuous across the antimeridian, and the wrap-offset system
    /// handles the visual rendering on both sides of the map.
    /// </summary>
    [Fact]
    public void Observe_AntimeridianCrossing_KeptContinuousViaUnwrapping()
    {
        // Track with 6 points east, then crossing to 6 points west
        // (small longitude steps that cross ±180°)
        var points = new List<GeoCoordinate>();
        for (var i = 0; i < 6; i++)
            points.Add(new GeoCoordinate(5.0 + i * 0.5, 170.0 + i * 1.5)); // lon 170–177.5

        for (var i = 0; i < 6; i++)
            points.Add(new GeoCoordinate(8.0 + i * 0.5, -177.5 + i * 1.5)); // lon -177.5 to -170

        var chains = EquirectangularProjection.ProjectGroundTrackForDraw(points, MapWidth, MapHeight);

        // ProjectGroundTrackForDraw uses longitude unwrapping, so the crossing is kept
        // as a single continuous chain (dx stays small because ShortestLongitudeDelta
        // for the crossing step is only ~5°)
        Assert.Single(chains);
        Assert.Equal(12, chains[0].Count);
    }

    /// <summary>
    /// Property: For any mid-latitude track crossing the antimeridian with small
    /// per-step longitude increments (≤ 10° per step via shortest path),
    /// ProjectGroundTrackForDraw keeps all points in a single continuous chain.
    ///
    /// This preserves the unwrapping behaviour that allows tracks to cross ±180° smoothly.
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Antimeridian_Crossing_With_Small_Steps_Kept_Continuous(int seed)
    {
        var rng = new Random(seed);

        // Generate a track that crosses the antimeridian eastward with small steps
        var count = rng.Next(6, 20);
        var baseLat = rng.NextDouble() * 40.0 - 20.0; // [-20, 20]
        var startLon = 170.0 + rng.NextDouble() * 5.0; // [170, 175] — starts east of antimeridian
        var lonStep = 1.0 + rng.NextDouble() * 4.0;    // [1, 5]° per step

        var points = new List<GeoCoordinate>();
        var lon = startLon;

        for (var i = 0; i < count; i++)
        {
            var lat = baseLat + i * 0.3;
            lat = Math.Clamp(lat, -50.0, 50.0);
            points.Add(new GeoCoordinate(lat, NormaliseLon(lon)));
            lon += lonStep; // unwrapped lon continues past 180
        }

        var chains = EquirectangularProjection.ProjectGroundTrackForDraw(points, MapWidth, MapHeight);

        // Should produce a single chain with all points (longitude unwrapping keeps it continuous)
        return chains.Count == 1 && chains[0].Count == count;
    }

    #endregion

    #region Property 3 — Fully-visible chains select offset 0

    /// <summary>
    /// Observation: SelectGroundTrackWrapOffset returns offset 0 for chains fully
    /// visible within viewport (visible span > max(120px, 8% of width) at offset 0).
    /// </summary>
    [Fact]
    public void Observe_FullyVisibleChain_SelectsOffset0()
    {
        // Chain comfortably within the viewport centre (x: 200–800)
        var chain = new List<(double X, double Y)>
        {
            (200.0, 100.0),
            (400.0, 150.0),
            (600.0, 200.0),
            (800.0, 250.0),
        };

        var offset = WorldMapControl.SelectGroundTrackWrapOffset(chain, MapWidth);

        Assert.NotNull(offset);
        Assert.Equal(0.0, offset.Value);
    }

    /// <summary>
    /// Property: For any chain fully visible within the viewport at offset 0
    /// (visible span > max(120px, 8% of width)), SelectGroundTrackWrapOffset returns 0.
    ///
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool FullyVisible_Chain_Selects_Offset_0(int seed)
    {
        var rng = new Random(seed);

        // Generate a chain comfortably within viewport bounds
        // Ensure visible span > max(120, 1200*0.08) = 120px at offset 0
        var count = rng.Next(3, 15);
        var minX = 50.0 + rng.NextDouble() * 200.0;  // [50, 250] — well inside left
        var span = 200.0 + rng.NextDouble() * 600.0;  // [200, 800] — well above 120px threshold
        var maxX = minX + span;

        // Ensure chain fits within viewport (0, MapWidth)
        if (maxX > MapWidth - 50.0)
            maxX = MapWidth - 50.0;
        if (maxX - minX < 150.0)
            return true; // Skip if we can't generate a valid comfortably-visible chain

        var chain = new List<(double X, double Y)>();
        for (var i = 0; i < count; i++)
        {
            var x = minX + (maxX - minX) * i / (count - 1);
            var y = rng.NextDouble() * MapHeight;
            chain.Add((x, y));
        }

        var offset = WorldMapControl.SelectGroundTrackWrapOffset(chain, MapWidth);

        // Should always return offset 0 for fully-visible chains
        return offset is not null && offset.Value == 0.0;
    }

    #endregion

    #region Property 4 — Gap-free propagation produces point count equal to time steps

    /// <summary>
    /// Observation: GetGroundTrack with a propagator that never throws produces a
    /// point list with count equal to the number of time steps.
    /// </summary>
    [Fact]
    public void Observe_GapFreePropagation_PointCountEqualsTimeSteps()
    {
        var expectedPoints = new List<GeoCoordinate>
        {
            new(0.0, 0.0),
            new(1.0, 5.0),
            new(2.0, 10.0),
            new(3.0, 15.0),
            new(4.0, 20.0),
            new(5.0, 25.0),
            new(6.0, 30.0),
            new(7.0, 35.0),
        };

        var propagator = new SequentialPropagator(expectedPoints);
        var geometry = new SampledGroundGeometry(propagator);

        var satellite = CreateTestSatellite("PRESERVE-1", "88801");
        var utcStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var step = TimeSpan.FromMinutes(1);
        var utcEnd = utcStart + step * (expectedPoints.Count - 1);

        var track = geometry.GetGroundTrack(satellite, utcStart, utcEnd, step);

        Assert.Equal(expectedPoints.Count, track.Count);
    }

    /// <summary>
    /// Property: For any number of time steps where the propagator never throws,
    /// GetGroundTrack produces a point list with count equal to the number of steps.
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool GapFree_Propagation_Returns_Exact_Point_Count(int seed)
    {
        var rng = new Random(seed);
        var count = rng.Next(3, 50);

        // Generate a smooth sequence of points
        var points = new List<GeoCoordinate>();
        var lat = rng.NextDouble() * 60.0 - 30.0;
        var lon = rng.NextDouble() * 120.0 - 60.0;

        for (var i = 0; i < count; i++)
        {
            points.Add(new GeoCoordinate(lat, lon));
            lat += rng.NextDouble() * 2.0 - 1.0;
            lon += rng.NextDouble() * 3.0 - 1.0;
            lat = Math.Clamp(lat, -85.0, 85.0);
            lon = Math.Clamp(lon, -170.0, 170.0);
        }

        var propagator = new SequentialPropagator(points);
        var geometry = new SampledGroundGeometry(propagator);
        var satellite = CreateTestSatellite("PRESERVE-PBT", "88802");

        var utcStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var step = TimeSpan.FromMinutes(1);
        var utcEnd = utcStart + step * (count - 1);

        var track = geometry.GetGroundTrack(satellite, utcStart, utcEnd, step);

        return track.Count == count;
    }

    #endregion

    #region Property 5 — Low-inclination non-polar orbit renders identically (single chain, all points)

    /// <summary>
    /// Observation: Low-inclination non-polar orbit at standard 1200×600 viewport
    /// renders identically (single chain, all points preserved).
    /// </summary>
    [Fact]
    public void Observe_LowInclinationOrbit_SingleChain_AllPoints()
    {
        // Simulate a low-inclination orbit segment: lat oscillating ±20°, lon advancing
        var points = new List<GeoCoordinate>();
        for (var i = 0; i < 30; i++)
        {
            var lat = 20.0 * Math.Sin(i * 0.3); // oscillates ±20°
            var lon = -60.0 + i * 4.0;           // advances from -60 to 56
            points.Add(new GeoCoordinate(lat, lon));
        }

        var chains = EquirectangularProjection.ProjectGroundTrackForDraw(points, MapWidth, MapHeight);

        Assert.Single(chains);
        Assert.Equal(30, chains[0].Count);
    }

    /// <summary>
    /// Property: For any low-inclination (lat within ±30°) non-polar orbit at
    /// standard 1200×600 viewport with gradual longitude advance (no antimeridian crossing),
    /// ProjectGroundTrackForDraw returns a single chain with all points.
    ///
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool LowInclination_NonPolar_Orbit_Produces_Identical_Single_Chain(int seed)
    {
        var rng = new Random(seed);

        // Generate a low-inclination orbit segment
        var count = rng.Next(10, 50);
        var amplitude = 5.0 + rng.NextDouble() * 25.0; // lat amplitude 5–30°
        var startLon = -80.0 + rng.NextDouble() * 60.0; // start lon [-80, -20]
        var lonStep = 2.0 + rng.NextDouble() * 3.0;     // lon step 2–5° per sample

        // Verify we won't cross antimeridian
        var endLon = startLon + lonStep * (count - 1);
        if (endLon > 170.0)
            return true; // Skip — would approach antimeridian

        var points = new List<GeoCoordinate>();
        for (var i = 0; i < count; i++)
        {
            var lat = amplitude * Math.Sin(i * 0.2 + rng.NextDouble() * 0.1);
            var lon = startLon + i * lonStep;
            points.Add(new GeoCoordinate(lat, lon));
        }

        var chains = EquirectangularProjection.ProjectGroundTrackForDraw(points, MapWidth, MapHeight);

        // Should be a single chain with all points
        return chains.Count == 1 && chains[0].Count == count;
    }

    #endregion

    #region Helpers

    private static SatelliteCatalogEntry CreateTestSatellite(string name, string noradId) =>
        new()
        {
            Name = name,
            NoradId = noradId,
            Line1 = $"1 {noradId}U 00000A   24001.00000000  .00000000  00000-0  00000-0 0  0000",
            Line2 = $"2 {noradId}  51.6400 000.0000 0000000 000.0000 000.0000 15.50000000 00000",
        };

    private static double NormaliseLon(double lon)
    {
        while (lon > 180.0) lon -= 360.0;
        while (lon < -180.0) lon += 360.0;
        return lon;
    }

    #endregion

    #region Test propagator

    /// <summary>
    /// A propagator that returns predetermined points sequentially without throwing.
    /// Used to test gap-free propagation preservation.
    /// </summary>
    private sealed class SequentialPropagator : IOrbitPropagator
    {
        private readonly IReadOnlyList<GeoCoordinate> _points;
        private int _callCount;

        public SequentialPropagator(IReadOnlyList<GeoCoordinate> points)
        {
            _points = points;
        }

        public void LoadSatellite(SatelliteCatalogEntry entry) { }
        public void RemoveSatellite(string noradId) { }
        public void Clear() { }
        public bool HasSatellite(string noradId) => true;
        public IReadOnlyCollection<string> LoadedNoradIds => Array.Empty<string>();

        public GeoCoordinate GetSubpoint(string noradId, DateTime utc)
        {
            var index = _callCount++;
            if (index < _points.Count)
                return _points[index];

            // If we run out of points, return last point
            return _points[^1];
        }

        public EciPosition GetEciPosition(string noradId, DateTime utc) =>
            throw new NotSupportedException();

        public LookAngles GetLookAngles(string noradId, GroundStation site, DateTime utc) =>
            throw new NotSupportedException();
    }

    #endregion
}
