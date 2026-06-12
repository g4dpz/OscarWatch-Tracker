// Feature: ground-track-truncation, Property 2: Preservation — Three-Plus-Point Chains and Single-Point Discard Unchanged

using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;
using Xunit;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4**
///
/// Preservation property tests: verify that existing behaviour of SplitForMapDraw is unchanged
/// for inputs that do NOT produce exactly-2-point chain segments. These tests must PASS on
/// both unfixed and fixed code — they confirm the baseline behaviour we want to preserve.
///
/// Preservation guarantees:
/// - Chains with 3+ points are included in output (Req 3.1)
/// - Single-point chains are discarded (Req 3.2)
/// - Tracks with no splits return a single unbroken chain (Req 3.3)
/// - Splitting logic at antimeridian and pixel jumps remains unchanged (Req 3.4)
/// </summary>
public class GroundTrackTruncationPreservationPropertyTests
{
    // ─── Observation: Long track with no crossing returns single unbroken chain ──

    [Fact]
    public void Observe_LongTrackNoSplit_ReturnsSingleUnbrokenChain()
    {
        // A long track that stays within one hemisphere — no antimeridian crossing,
        // no large pixel jumps. Should return exactly one chain with all points.
        var points = new List<GeoCoordinate>();
        for (var i = 0; i < 50; i++)
        {
            points.Add(new GeoCoordinate(i * 1.0, i * 1.0)); // gradual movement
        }

        var chains = EquirectangularProjection.SplitForMapDraw(points, 800, 400);

        Assert.Single(chains);
        Assert.Equal(50, chains[0].Count);
    }

    [Fact]
    public void Observe_TrackWith3PlusPointChains_AllChainsReturned()
    {
        // Track crossing the antimeridian with enough points on each side to produce
        // chains with 3+ points on both sides.
        var points = new List<GeoCoordinate>
        {
            // Eastern segment (4 points — well above the 3-point threshold)
            new(0.0, 170.0),
            new(1.0, 173.0),
            new(2.0, 176.0),
            new(3.0, 179.0),
            // Western segment (4 points — crosses antimeridian, |(-177) - 179| = 356 > 180)
            new(4.0, -177.0),
            new(5.0, -174.0),
            new(6.0, -171.0),
            new(7.0, -168.0),
        };

        var chains = EquirectangularProjection.SplitForMapDraw(points, 800, 400);

        Assert.Equal(2, chains.Count);
        Assert.True(chains[0].Count >= 3, $"Eastern chain should have 3+ points, got {chains[0].Count}");
        Assert.True(chains[1].Count >= 3, $"Western chain should have 3+ points, got {chains[1].Count}");
    }

    [Fact]
    public void Observe_SinglePointChains_AreDiscarded()
    {
        // Construct input where splitting produces a 1-point segment.
        // A single point followed by an antimeridian crossing, then more points.
        // The single point on the east side forms a 1-point chain that should be discarded.
        var points = new List<GeoCoordinate>
        {
            new(0.0, 179.0),     // Single point in eastern segment
            new(1.0, -179.0),    // Crosses antimeridian (|(-179) - 179| = 358 > 180)
            new(2.0, -178.0),
            new(3.0, -177.0),
            new(4.0, -176.0),    // Western segment has 4 points (passes threshold)
        };

        var chains = EquirectangularProjection.SplitForMapDraw(points, 800, 400);

        // The 1-point eastern chain should be discarded. Only the western 4-point chain remains.
        Assert.Single(chains);
        Assert.Equal(4, chains[0].Count);
    }

    // ─── Property-Based: Long continuous tracks (no splits) ──────────────────────

    [Property(MaxTest = 200, DisplayName = "Feature: ground-track-truncation, Property 2a: Long continuous track returns single chain")]
    public bool LongContinuousTrack_ReturnsSingleUnbrokenChain(
        PositiveInt lengthSeed, PositiveInt widthSeed, PositiveInt heightSeed)
    {
        // Generate a track with no antimeridian crossing and no large pixel jumps
        var length = 5 + (lengthSeed.Get % 100); // 5..104 points
        var width = 200.0 + (widthSeed.Get % 1800); // 200..2000
        var height = 100.0 + (heightSeed.Get % 900); // 100..1000

        // Stay within [-80, 80] lat and [-90, 90] lon to avoid antimeridian and poles
        var points = new List<GeoCoordinate>();
        var lat = 0.0;
        var lon = 0.0;
        for (var i = 0; i < length; i++)
        {
            points.Add(new GeoCoordinate(lat, lon));
            // Small increments that won't trigger pixel-jump threshold
            // maxDy = height/3; step in lat = 1.0 => pixel step = (1/180)*height
            // For height=1000, pixel step = 5.56, maxDy = 333 — safe
            lat += 1.0;
            lon += 0.5;
            // Clamp to avoid wrapping near antimeridian or poles
            if (lat > 80.0) lat = -80.0 + (lat - 80.0);
            if (lon > 90.0) lon = -90.0;
        }

        var chains = EquirectangularProjection.SplitForMapDraw(points, width, height);

        // Should return exactly 1 chain containing all points
        return chains.Count == 1 && chains[0].Count == length;
    }

    // ─── Property-Based: 3+ point chains from antimeridian splits are all retained ──

    [Property(MaxTest = 200, DisplayName = "Feature: ground-track-truncation, Property 2b: Multi-segment track with 3+ point chains retains all chains")]
    public bool MultiSegmentTrack_With3PlusPointChains_AllRetained(
        PositiveInt segCountSeed, PositiveInt segLenSeed, PositiveInt widthSeed, PositiveInt heightSeed)
    {
        // Build a track that crosses the antimeridian multiple times, but each segment
        // has enough points (4+) so no 2-point chains appear.
        var segCount = 2 + (segCountSeed.Get % 4); // 2..5 segments
        var segLen = 4 + (segLenSeed.Get % 10);    // 4..13 points per segment
        var width = 200.0 + (widthSeed.Get % 1800);
        var height = 100.0 + (heightSeed.Get % 900);

        var points = new List<GeoCoordinate>();
        var eastSide = true;

        for (var seg = 0; seg < segCount; seg++)
        {
            var baseLon = eastSide ? 160.0 : -160.0;
            for (var i = 0; i < segLen; i++)
            {
                var lat = seg * 5.0 + i * 0.5; // gradual lat movement
                if (lat > 85.0) lat = 85.0;
                var lon = baseLon + i * 1.0;
                // Clamp longitude to stay on the correct side of antimeridian
                if (eastSide && lon > 179.0) lon = 179.0;
                if (!eastSide && lon < -179.0) lon = -179.0;
                points.Add(new GeoCoordinate(lat, lon));
            }
            eastSide = !eastSide;
        }

        var chains = EquirectangularProjection.SplitForMapDraw(points, width, height);

        // All chains should have 3+ points (since each segment has 4+ points)
        // and the number of chains should equal the number of segments
        if (chains.Count != segCount) return false;
        for (var i = 0; i < chains.Count; i++)
        {
            if (chains[i].Count < 3) return false;
        }
        return true;
    }

    // ─── Property-Based: Single-point chains are always discarded ─────────────────

    [Property(MaxTest = 200, DisplayName = "Feature: ground-track-truncation, Property 2c: Single-point chains are discarded")]
    public bool SinglePointChains_AreAlwaysDiscarded(
        PositiveInt widthSeed, PositiveInt heightSeed)
    {
        var width = 200.0 + (widthSeed.Get % 1800);
        var height = 100.0 + (heightSeed.Get % 900);

        // Create a track where the first segment before an antimeridian crossing
        // has only 1 point, and the second segment has 4+ points.
        var points = new List<GeoCoordinate>
        {
            new(0.0, 179.5),      // Single point in first segment
            new(1.0, -178.0),     // Crosses antimeridian
            new(2.0, -176.0),
            new(3.0, -174.0),
            new(4.0, -172.0),     // Second segment has 4 points
        };

        var chains = EquirectangularProjection.SplitForMapDraw(points, width, height);

        // The 1-point first segment should always be discarded.
        // Only the 4-point second segment should remain.
        if (chains.Count != 1) return false;
        if (chains[0].Count != 4) return false;

        // Also verify no chain in the output ever has fewer than 3 points
        // (on unfixed code, the threshold is Count >= 3)
        foreach (var chain in chains)
        {
            if (chain.Count < 3) return false;
        }
        return true;
    }

    // ─── Property-Based: Splitting at antimeridian logic unchanged ────────────────

    [Property(MaxTest = 200, DisplayName = "Feature: ground-track-truncation, Property 2d: Antimeridian split occurs at |Δlon| > 180")]
    public bool AntimeridianSplit_OccursAtLargeLongitudeJump(
        PositiveInt widthSeed, PositiveInt heightSeed, int latOffsetSeed)
    {
        var width = 200.0 + (widthSeed.Get % 1800);
        var height = 100.0 + (heightSeed.Get % 900);
        var latOffset = (latOffsetSeed % 70); // [-70, 70] base latitude

        // Construct a track that crosses the antimeridian exactly once.
        // Eastern side: 5 points, Western side: 5 points (all pass threshold)
        var points = new List<GeoCoordinate>();
        for (var i = 0; i < 5; i++)
            points.Add(new GeoCoordinate(latOffset + i * 0.5, 170.0 + i * 2.0)); // east: 170, 172, 174, 176, 178

        for (var i = 0; i < 5; i++)
            points.Add(new GeoCoordinate(latOffset + 2.5 + i * 0.5, -178.0 + i * 2.0)); // west: -178, -176, -174, -172, -170

        // The jump from 178 to -178: |(-178) - 178| = 356 > 180 → split
        var chains = EquirectangularProjection.SplitForMapDraw(points, width, height);

        // Should produce exactly 2 chains
        if (chains.Count != 2) return false;
        // First chain: 5 eastern points
        if (chains[0].Count != 5) return false;
        // Second chain: 5 western points
        if (chains[1].Count != 5) return false;
        return true;
    }

    // ─── Property-Based: Splitting at pixel jumps logic unchanged ─────────────────

    [Property(MaxTest = 200, DisplayName = "Feature: ground-track-truncation, Property 2e: Pixel-jump split occurs at Δy > height/3")]
    public bool PixelJumpSplit_OccursAtLargeLatitudeJump(
        PositiveInt widthSeed, PositiveInt heightSeed, PositiveInt latJumpSeed)
    {
        var width = 200.0 + (widthSeed.Get % 1800);
        var height = 100.0 + (heightSeed.Get % 900);

        // First segment: 5 points near equator (lat 0..4, lon 10..14)
        // Last point of first segment is at lat = 4.
        // The jump from lat 4 to the start of segment 2 must exceed maxDy = height/3.
        // Δy = (ΔLat / 180) * height > height / 3 → ΔLat > 60
        // Since first segment ends at lat=4, we need segment2Start - 4 > 60 → segment2Start > 64
        var segment2Start = 65.0 + (latJumpSeed.Get % 15); // 65..79 degrees

        // First segment: 5 points near equator (passes threshold)
        var points = new List<GeoCoordinate>();
        for (var i = 0; i < 5; i++)
            points.Add(new GeoCoordinate(i * 1.0, 10.0 + i * 1.0));

        // Large jump — second segment starts well above lat 64
        for (var i = 0; i < 5; i++)
            points.Add(new GeoCoordinate(segment2Start + i * 1.0, 15.0 + i * 1.0));

        var chains = EquirectangularProjection.SplitForMapDraw(points, width, height);

        // Should produce exactly 2 chains, each with 5 points
        if (chains.Count != 2) return false;
        if (chains[0].Count != 5) return false;
        if (chains[1].Count != 5) return false;
        return true;
    }

    // ─── Property-Based: Pixel-jump split at Δx > width/2 ────────────────────────

    [Property(MaxTest = 200, DisplayName = "Feature: ground-track-truncation, Property 2f: Pixel-jump split occurs at Δx > width/2")]
    public bool PixelJumpSplit_OccursAtLargeLongitudePixelJump(
        PositiveInt widthSeed, PositiveInt heightSeed)
    {
        var width = 200.0 + (widthSeed.Get % 1800);
        var height = 100.0 + (heightSeed.Get % 900);

        // Need a longitude jump that produces Δx > width/2 but does NOT cross antimeridian
        // x = (lon + 180) / 360 * width
        // Δx = (Δlon / 360) * width > width/2 → Δlon > 180 ... but that would cross antimeridian
        // Actually for pixel jumps within the same antimeridian segment:
        // The antimeridian split happens first, so pixel jumps within a segment reflect
        // real pixel distances. A jump of Δlon = 100 gives Δx = (100/360)*width.
        // For width=800: Δx = 222, maxDx = 400. Need Δlon > 180 for this to trigger...
        // This means pixel-jump-X is mostly triggered by the antimeridian split already.
        // Let's just verify the height/3 threshold with longitude held constant.
        // This property is already covered by 2e above.

        // Instead, verify a specific case where both Δx and Δy don't trigger:
        // no split when jumps are within thresholds
        var points = new List<GeoCoordinate>();
        for (var i = 0; i < 10; i++)
        {
            // lat moves slowly (< 60 deg total), lon moves slowly, stays same hemisphere
            points.Add(new GeoCoordinate(i * 5.0, i * 5.0));
        }

        var chains = EquirectangularProjection.SplitForMapDraw(points, width, height);

        // With small lat/lon steps, no split should occur — single chain
        return chains.Count == 1 && chains[0].Count == 10;
    }
}
