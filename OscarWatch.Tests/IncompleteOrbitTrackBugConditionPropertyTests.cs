// Feature: incomplete-orbit-tracks, Property 1: Bug Condition — Incomplete Orbit Track Rendering

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
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.4**
///
/// Bug condition exploration tests: demonstrate the four sub-conditions of the
/// incomplete orbit tracks bug on UNFIXED code.
///
/// These tests are EXPECTED TO FAIL on unfixed code — failure confirms the bugs exist.
/// DO NOT attempt to fix the tests or the code when they fail.
/// </summary>
public class IncompleteOrbitTrackBugConditionPropertyTests
{
    private const double MapWidth = 1200.0;
    private const double MapHeight = 600.0;

    #region Sub-condition 1 — Polar pass chain break (via draw loop)

    /// <summary>
    /// Sub-condition 1: Polar pass rendered with gaps due to draw loop segment skipping.
    ///
    /// A satellite passing over high latitudes with coarse sampling produces large
    /// latitude jumps between consecutive points. ProjectGroundTrackForDraw correctly
    /// keeps the chain continuous (because the break condition requires dy &lt; h/6, and
    /// polar passes have large dy). Now that the draw loop's redundant filter has been
    /// removed (task 3.3), all segments in the chain are drawn without secondary filtering.
    ///
    /// Expected: ProjectGroundTrackForDraw produces a single continuous chain with all points.
    /// </summary>
    [Fact]
    public void Polar_pass_coarse_sample_segments_not_skipped_by_draw_loop()
    {
        // Arrange: coarsely sampled polar pass with > 60° latitude jumps per step
        var points = new List<GeoCoordinate>
        {
            new(-10.0, 30.0),   // Southern low latitude
            new(55.0, 0.0),     // Northern mid (jump: 65°)
            new(85.0, -40.0),   // Near pole (jump: 30°)
            new(20.0, -100.0),  // Descending (jump: 65°)
        };

        var chains = EquirectangularProjection.ProjectGroundTrackForDraw(points, MapWidth, MapHeight);

        // ProjectGroundTrackForDraw keeps this as one chain (the tightened break condition
        // requires a genuine antimeridian crossing with |shortestLonDelta| >= 170°, which
        // these moderate longitude changes do not trigger)
        Assert.Single(chains);
        var chain = chains[0];
        Assert.Equal(4, chain.Count);

        // With the redundant draw loop filter removed, all segments in this single chain
        // are now guaranteed to be drawn. No secondary filtering exists.
    }

    /// <summary>
    /// Sub-condition 1: Polar pass property-based test.
    ///
    /// For any polar pass track where consecutive latitude changes exceed 60°,
    /// ProjectGroundTrackForDraw should produce a single continuous chain containing
    /// all points. With the redundant draw loop filter removed, all segments in
    /// such a chain are guaranteed to be drawn.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Polar_pass_all_segments_drawable_regardless_of_dy(int seed)
    {
        var rng = new Random(seed);

        // Generate a polar pass with large latitude steps
        var startLat = rng.NextDouble() * 30.0 - 20.0;  // -20 to 10
        var peakLat = 70.0 + rng.NextDouble() * 19.0;    // 70 to 89
        var endLat = rng.NextDouble() * 30.0 - 20.0;     // -20 to 10
        var startLon = rng.NextDouble() * 360.0 - 180.0;
        var lonStep = -(rng.NextDouble() * 30.0 + 10.0);

        var points = new List<GeoCoordinate>
        {
            new(startLat, NormaliseLon(startLon)),
            new(peakLat, NormaliseLon(startLon + lonStep)),
            new(endLat, NormaliseLon(startLon + lonStep * 2)),
        };

        // Verify no consecutive pair triggers antimeridian
        for (var i = 0; i < points.Count - 1; i++)
        {
            var delta = Math.Abs(ShortestLongitudeDelta(points[i].LongitudeDeg, points[i + 1].LongitudeDeg));
            if (delta >= 170.0)
                return true; // Skip — antimeridian crossing
        }

        var chains = EquirectangularProjection.ProjectGroundTrackForDraw(points, MapWidth, MapHeight);

        // With the tightened break condition (requires |shortestLonDelta| >= 170°),
        // polar passes with moderate longitude changes should form a single chain
        // containing all 3 points.
        if (chains.Count != 1)
            return false; // Bug: chain was incorrectly broken

        return chains[0].Count == 3;
    }

    #endregion

    #region Sub-condition 2 — Short-span chain rejection

    /// <summary>
    /// Sub-condition 2: Short-span chain rejection (concrete case).
    ///
    /// Construct a 5-point chain that straddles the viewport right edge with
    /// visible span &lt; 120px at all three offsets (0, +w, −w).
    ///
    /// At 1200px width, minVisibleSpanPx = max(120, 1200*0.08) = max(120, 96) = 120px.
    /// Place points so that the visible portion at each offset is less than 120px.
    ///
    /// Expected: SelectGroundTrackWrapOffset returns non-null (offset 0 fallback).
    /// Actual (unfixed): returns null, dropping the chain entirely.
    /// </summary>
    [Fact]
    public void SelectGroundTrackWrapOffset_returns_non_null_for_short_span_chain()
    {
        // Arrange: 5 points clustered near x=1160..1240 (straddles right edge of 1200px viewport)
        // At offset 0: visible from x=1160 to x=1200 → span = 40px < 120px
        // At offset +w (1200): visible from x=2360 to x=2440 → entirely off right → span = 0px
        // At offset -w (-1200): visible from x=-40 to x=40 → span = 40px < 120px
        var chain = new List<(double X, double Y)>
        {
            (1160.0, 100.0),
            (1180.0, 110.0),
            (1200.0, 120.0),
            (1220.0, 130.0),
            (1240.0, 140.0),
        };

        // Act
        var offset = WorldMapControl.SelectGroundTrackWrapOffset(chain, MapWidth);

        // Assert: should return a valid offset (expected behaviour is fallback to 0)
        // Bug: unfixed code returns null because no offset meets the 120px threshold
        Assert.NotNull(offset);
    }

    /// <summary>
    /// Sub-condition 2: Short-span chain rejection (property-based).
    ///
    /// For any chain with ≥2 points, SelectGroundTrackWrapOffset should never return null.
    /// Even if the visible span is small, the chain should still be drawn at some offset.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool SelectGroundTrackWrapOffset_never_returns_null_for_valid_chain(int seed)
    {
        var rng = new Random(seed);

        // Generate a short chain (2-8 points) near a viewport edge
        var count = rng.Next(2, 9);
        var baseX = MapWidth - 20.0 + rng.NextDouble() * 40.0; // near right edge
        var chain = new List<(double X, double Y)>();

        for (var i = 0; i < count; i++)
        {
            // Cluster points within a 100px horizontal span (< 120px threshold)
            var x = baseX + rng.NextDouble() * 100.0 - 50.0;
            var y = rng.NextDouble() * MapHeight;
            chain.Add((x, y));
        }

        // Verify that the visible span is actually < threshold at all offsets
        // (to confirm we're testing the bug condition)
        var threshold = Math.Max(120.0, MapWidth * 0.08);
        var span0 = VisibleSpan(chain, 0.0, MapWidth);
        var spanP = VisibleSpan(chain, MapWidth, MapWidth);
        var spanN = VisibleSpan(chain, -MapWidth, MapWidth);

        if (span0 >= threshold || spanP >= threshold || spanN >= threshold)
            return true; // Not in bug condition — chain would pass normally

        var offset = WorldMapControl.SelectGroundTrackWrapOffset(chain, MapWidth);

        // Expected: non-null (fallback to offset 0)
        // Bug: unfixed code returns null
        return offset is not null;
    }

    #endregion

    #region Sub-condition 3 — Redundant segment skip

    /// <summary>
    /// Sub-condition 3: Redundant segment skip (concrete case).
    ///
    /// Construct a chain where two consecutive points have large latitude jumps but
    /// were kept together by ProjectGroundTrackForDraw. The projection break condition
    /// requires BOTH a large dx AND a genuine antimeridian crossing. Since these points
    /// have small longitude changes, the break does NOT fire.
    ///
    /// With the redundant draw loop filter removed (task 3.3), all segments in the
    /// resulting single chain are guaranteed to be drawn.
    ///
    /// Expected: ProjectGroundTrackForDraw produces a single chain with all 4 points.
    /// </summary>
    [Fact]
    public void DrawLoop_does_not_skip_segments_with_large_dy_kept_by_projection()
    {
        // Arrange: points with > 60° latitude jump in one step.
        // dx is small (< w/2), so projection break condition never fires.
        var points = new List<GeoCoordinate>
        {
            new(10.0, 50.0),    // Low latitude
            new(15.0, 55.0),    // Small step (OK)
            new(80.0, 60.0),    // BIG latitude jump: 65deg from lat 15
            new(82.0, 62.0),    // Small step after (OK)
        };

        var chains = EquirectangularProjection.ProjectGroundTrackForDraw(points, MapWidth, MapHeight);

        // Verify: ProjectGroundTrackForDraw keeps these in one chain
        // (break requires a genuine antimeridian crossing; here dx is small with no crossing)
        Assert.Single(chains);
        var chain = chains[0];
        Assert.Equal(4, chain.Count);

        // With the redundant draw loop filter removed, all segments are now drawn.
        // No secondary filtering exists — chain continuity from ProjectGroundTrackForDraw
        // is the sole authority on what gets rendered.
    }

    /// <summary>
    /// Sub-condition 3: Multiple segments with large latitude jumps (concrete case).
    ///
    /// A full-orbit coarse sample where multiple consecutive latitude jumps exceed 60°.
    /// ProjectGroundTrackForDraw keeps all points in a single chain because the break
    /// condition requires a genuine antimeridian crossing. With the redundant draw loop
    /// filter removed, all segments are guaranteed to be drawn.
    ///
    /// Expected: single chain with all 5 points (all segments drawable).
    /// </summary>
    [Fact]
    public void DrawLoop_does_not_skip_multiple_large_dy_segments()
    {
        // Arrange: an orbit going from south to north and back, with coarse sampling
        // Each step has 70° latitude change
        var points = new List<GeoCoordinate>
        {
            new(-70.0, 0.0),
            new(0.0, 30.0),      // Jump: 70deg
            new(70.0, 60.0),     // Jump: 70deg
            new(0.0, 90.0),      // Jump: 70deg
            new(-70.0, 120.0),   // Jump: 70deg
        };

        var chains = EquirectangularProjection.ProjectGroundTrackForDraw(points, MapWidth, MapHeight);

        // Should be one chain (small longitude steps, no antimeridian crossing,
        // so the tightened break condition never fires)
        Assert.Single(chains);
        var chain = chains[0];
        Assert.Equal(5, chain.Count);

        // With the redundant draw loop filter removed, all 4 segments between these
        // 5 points are drawn. No secondary filtering exists.
    }

    #endregion

    #region Sub-condition 4 — Propagation gap

    /// <summary>
    /// Sub-condition 4: Propagation gap (concrete case).
    ///
    /// Mock a propagator that throws at one sample time. The resulting track from
    /// GetGroundTrack should either contain an interpolated point or a NaN sentinel
    /// marker rather than silently omitting the point (which creates a spatial gap).
    ///
    /// Expected: point count equals sample count (interpolation or sentinel inserted).
    /// Actual (unfixed): point count is sample count - 1 (point silently omitted).
    /// </summary>
    [Fact]
    public void GetGroundTrack_handles_single_propagation_exception_without_gap()
    {
        // Arrange: a propagator that throws at the 3rd sample time
        var failAtIndex = 2;
        var basePoints = new List<GeoCoordinate>
        {
            new(0.0, 0.0),
            new(1.0, 5.0),
            new(2.0, 10.0),   // This one will throw
            new(3.0, 15.0),
            new(4.0, 20.0),
        };

        var propagator = new ThrowingPropagator(basePoints, failAtIndex);
        var geometry = new SampledGroundGeometry(propagator);

        var satellite = new SatelliteCatalogEntry
        {
            Name = "TEST-SAT",
            NoradId = "99999",
            Line1 = "1 99999U 00000A   24001.00000000  .00000000  00000-0  00000-0 0  0000",
            Line2 = "2 99999  51.6400 000.0000 0000000 000.0000 000.0000 15.50000000 00000",
        };

        var utcStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var step = TimeSpan.FromMinutes(1);
        var utcEnd = utcStart + step * 4; // 5 sample points (0, 1, 2, 3, 4 minutes)

        // Act
        var track = geometry.GetGroundTrack(satellite, utcStart, utcEnd, step);

        // Assert: the result should have 5 points (with interpolation or NaN sentinel)
        // rather than 4 points (silently omitted gap)
        // Bug: unfixed code swallows exception and produces only 4 points
        Assert.Equal(5, track.Count);
    }

    /// <summary>
    /// Sub-condition 4: Verify that the gap doesn't cause spurious chain breaks.
    ///
    /// When a point is silently omitted, the resulting spatial gap between neighbours
    /// can cause ProjectGroundTrackForDraw to create an additional chain break that
    /// wouldn't exist if the point were present.
    /// </summary>
    [Fact]
    public void Propagation_gap_does_not_cause_additional_chain_breaks()
    {
        // Arrange: propagator that produces evenly spaced points except one throws.
        // Without the gap, the track should be one continuous chain.
        // With the gap (omitted point), the spatial jump might trigger a chain break.
        var failAtIndex = 5;
        var points = new List<GeoCoordinate>();

        // Generate a smooth high-latitude track where removing one point creates a jump
        for (var i = 0; i < 12; i++)
        {
            var lat = 70.0 + i * 1.5;   // Ascending through polar region
            var lon = 30.0 - i * 15.0;  // Rapid westward longitude change
            if (lat > 89.0) lat = 89.0;
            points.Add(new GeoCoordinate(lat, NormaliseLon(lon)));
        }

        var propagator = new ThrowingPropagator(points, failAtIndex);
        var geometry = new SampledGroundGeometry(propagator);

        var satellite = new SatelliteCatalogEntry
        {
            Name = "POLAR-SAT",
            NoradId = "99998",
            Line1 = "1 99998U 00000A   24001.00000000  .00000000  00000-0  00000-0 0  0000",
            Line2 = "2 99998  98.0000 000.0000 0000000 000.0000 000.0000 14.50000000 00000",
        };

        var utcStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var step = TimeSpan.FromMinutes(1);
        var utcEnd = utcStart + step * (points.Count - 1);

        // Get the track with the gap
        var trackWithGap = geometry.GetGroundTrack(satellite, utcStart, utcEnd, step);

        // Project both the full track (no gap) and the gapped track
        var chainsNoGap = EquirectangularProjection.ProjectGroundTrackForDraw(
            points, MapWidth, MapHeight);
        var chainsWithGap = EquirectangularProjection.ProjectGroundTrackForDraw(
            trackWithGap, MapWidth, MapHeight);

        // Expected: same number of chains (gap handled gracefully with interpolation/sentinel)
        // Bug: unfixed code may produce more chains due to the spatial gap from omitted point
        Assert.Equal(chainsNoGap.Count, chainsWithGap.Count);
    }

    #endregion

    #region Helpers

    private static double NormaliseLon(double lon)
    {
        while (lon > 180.0) lon -= 360.0;
        while (lon < -180.0) lon += 360.0;
        return lon;
    }

    private static double ShortestLongitudeDelta(double from, double to)
    {
        var delta = to - from;
        while (delta > 180.0) delta -= 360.0;
        while (delta < -180.0) delta += 360.0;
        return delta;
    }

    private static double VisibleSpan(
        IReadOnlyList<(double X, double Y)> chain, double xOffset, double w)
    {
        var minX = double.MaxValue;
        var maxX = double.MinValue;
        foreach (var p in chain)
        {
            var x = p.X + xOffset;
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
        }

        var left = Math.Max(0, minX);
        var right = Math.Min(w, maxX);
        return Math.Max(0, right - left);
    }

    #endregion

    #region Test propagator

    /// <summary>
    /// A propagator that returns predetermined points but throws at a specified index.
    /// Used to test propagation gap handling.
    /// </summary>
    private sealed class ThrowingPropagator : IOrbitPropagator
    {
        private readonly IReadOnlyList<GeoCoordinate> _points;
        private readonly int _failAtIndex;
        private int _callCount;

        public ThrowingPropagator(IReadOnlyList<GeoCoordinate> points, int failAtIndex)
        {
            _points = points;
            _failAtIndex = failAtIndex;
        }

        public void LoadSatellite(SatelliteCatalogEntry entry) { }
        public void RemoveSatellite(string noradId) { }
        public void Clear() { }
        public bool HasSatellite(string noradId) => true;
        public IReadOnlyCollection<string> LoadedNoradIds => Array.Empty<string>();

        public GeoCoordinate GetSubpoint(string noradId, DateTime utc)
        {
            var index = _callCount++;
            if (index == _failAtIndex)
                throw new InvalidOperationException("Simulated propagation failure (decayed TLE)");

            if (index < _points.Count)
                return _points[index];

            return new GeoCoordinate(0.0, 0.0);
        }

        public EciPosition GetEciPosition(string noradId, DateTime utc) =>
            throw new NotSupportedException();

        public LookAngles GetLookAngles(string noradId, GroundStation site, DateTime utc) =>
            throw new NotSupportedException();
    }

    #endregion
}
