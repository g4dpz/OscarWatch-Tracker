// Feature: ground-track-truncation, Property 1: Bug Condition — Two-Point Chains Discarded Near Antimeridian

using FsCheck.Xunit;
using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;
using Xunit;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 1.1, 1.2, 1.3, 2.1, 2.2, 2.3**
///
/// Bug condition exploration test: demonstrates that SplitForMapDraw discards valid
/// 2-point chain segments near antimeridian crossings and large pixel-distance jumps.
///
/// Expected behavior (design): all chain segments with Count >= 2 should be retained
/// in the returned list, since 2 points form a valid drawable line segment.
///
/// This test is EXPECTED TO FAIL on unfixed code — failure confirms the bug exists
/// because the Count >= 3 threshold silently drops 2-point chains.
/// </summary>
public class GroundTrackTruncationBugConditionPropertyTests
{
    private const double MapWidth = 800.0;
    private const double MapHeight = 400.0;

    /// <summary>
    /// Property 1: Bug Condition — Two-Point Chains Discarded Near Antimeridian
    ///
    /// Antimeridian crossing with short western chain: points at 178°E, 179°E cross
    /// to 175°W, 170°W. After splitting at the antimeridian, the western chain has
    /// exactly 2 points. The unfixed code discards this chain due to Count >= 3.
    ///
    /// Expected: SplitForMapDraw returns 2 chains (eastern with 2 points, western with 2 points).
    /// Actual (unfixed): returns 0 chains — both 2-point chains are dropped.
    /// </summary>
    [Fact]
    public void SplitForMapDraw_retains_two_point_chain_after_antimeridian_crossing()
    {
        // Arrange: 4 points crossing the antimeridian — eastern pair and western pair
        // The antimeridian split produces two 2-point chains
        var points = new List<GeoCoordinate>
        {
            new(0.0, 178.0),   // East of antimeridian
            new(0.0, 179.0),   // East of antimeridian
            new(0.0, -175.0),  // West of antimeridian (crosses: |(-175) - 179| = 354 > 180)
            new(0.0, -170.0),  // West of antimeridian
        };

        // Act
        var chains = EquirectangularProjection.SplitForMapDraw(points, MapWidth, MapHeight);

        // Assert: both 2-point chains should be retained (expected behavior)
        // Bug: unfixed code returns 0 chains because Count >= 3 discards both
        Assert.Equal(2, chains.Count);
        Assert.True(chains[0].Count >= 2, "Eastern chain should have at least 2 points");
        Assert.True(chains[1].Count >= 2, "Western chain should have at least 2 points");
    }

    /// <summary>
    /// Property 1: Bug Condition — Two-Point Leading Chain Discarded After Large Pixel Jump
    ///
    /// Two close points followed by a large latitude jump: the leading 2-point chain
    /// before the jump is discarded by the Count >= 3 threshold.
    ///
    /// Expected: SplitForMapDraw returns 2 chains (leading 2-point chain + trailing chain).
    /// Actual (unfixed): returns 1 chain — the 2-point leading chain is dropped.
    /// </summary>
    [Fact]
    public void SplitForMapDraw_retains_two_point_leading_chain_before_large_pixel_jump()
    {
        // Arrange: 2 close points then a large latitude jump followed by more points
        // maxDy = height / 3 = 400 / 3 ≈ 133.3 pixels
        // A jump from lat 0 to lat 80 produces Δy ≈ (80/180)*400 = 177.8 > 133.3
        var points = new List<GeoCoordinate>
        {
            new(0.0, 10.0),    // First point
            new(1.0, 11.0),    // Second point (close to first — forms a 2-point leading chain)
            new(80.0, 12.0),   // Large latitude jump — triggers pixel-jump split
            new(81.0, 13.0),   // Continues after jump
            new(82.0, 14.0),   // Continues after jump
            new(83.0, 15.0),   // Continues after jump (trailing chain has 4 points, passes Count >= 3)
        };

        // Act
        var chains = EquirectangularProjection.SplitForMapDraw(points, MapWidth, MapHeight);

        // Assert: both chains should be retained (expected behavior)
        // Bug: unfixed code returns 1 chain — the 2-point leading chain is dropped
        Assert.Equal(2, chains.Count);
        Assert.Equal(2, chains[0].Count); // Leading 2-point chain
        Assert.True(chains[1].Count >= 3, "Trailing chain should have 3+ points");
    }

    /// <summary>
    /// Property 1: Bug Condition — Multiple Two-Point Chains From Rapid Antimeridian Crossings
    ///
    /// Rapidly alternating antimeridian crossings producing multiple 2-point chains.
    /// All are discarded by the unfixed code, resulting in 0 visible chains.
    ///
    /// Expected: SplitForMapDraw returns all chains with Count >= 2.
    /// Actual (unfixed): returns 0 chains — all 2-point chains are dropped.
    /// </summary>
    [Fact]
    public void SplitForMapDraw_retains_multiple_two_point_chains_from_rapid_crossings()
    {
        // Arrange: rapidly alternating crossings producing three 2-point chains
        var points = new List<GeoCoordinate>
        {
            new(0.0, 179.0),   // Eastern segment chain 1
            new(1.0, 179.5),   // Eastern segment chain 1 (2 points)
            new(2.0, -179.5),  // Crosses to west — chain 2 starts (|(-179.5) - 179.5| = 359 > 180)
            new(3.0, -179.0),  // Western segment chain 2 (2 points)
            new(4.0, 179.0),   // Crosses back east — chain 3 starts (|179 - (-179)| = 358 > 180)
            new(5.0, 179.5),   // Eastern segment chain 3 (2 points)
        };

        // Act
        var chains = EquirectangularProjection.SplitForMapDraw(points, MapWidth, MapHeight);

        // Assert: all three 2-point chains should be retained (expected behavior)
        // Bug: unfixed code returns 0 chains — all are dropped by Count >= 3
        Assert.Equal(3, chains.Count);
        foreach (var chain in chains)
        {
            Assert.True(chain.Count >= 2, $"Each chain should have at least 2 points, got {chain.Count}");
        }
    }

    /// <summary>
    /// Property 1: Bug Condition (Property-Based) — Any Sequence Producing 2-Point Chains
    ///
    /// For any map dimensions and latitude offset that produce a valid pixel-jump split
    /// with a 2-point leading chain, SplitForMapDraw should retain that chain.
    ///
    /// Scoped PBT: generates varying map sizes and lat offsets to confirm the bug
    /// is systematic (not just one specific input).
    /// </summary>
    [Property(MaxTest = 100)]
    public bool SplitForMapDraw_retains_two_point_chains_for_any_valid_dimensions(
        int rawWidth, int rawHeight, double rawLatOffset)
    {
        // Discard NaN/Infinity inputs — not meaningful for map dimensions
        if (double.IsNaN(rawLatOffset) || double.IsInfinity(rawLatOffset))
            return true; // trivially satisfied — input is not in the domain

        // Constrain to positive map dimensions
        var width = (double)(Math.Abs(rawWidth % 2000) + 100);   // 100..2100
        var height = (double)(Math.Abs(rawHeight % 1000) + 100); // 100..1100

        // Need a latitude jump that STRICTLY exceeds maxDy = height / 3
        // Δy = (ΔLat / 180) * height > height / 3  =>  ΔLat > 60
        // Use ΔLat >= 65 to ensure we safely exceed the threshold (accounts for
        // floating-point equality at the boundary when ΔLat = 60 exactly)
        var latOffset = 65.0 + (Math.Abs(rawLatOffset % 20.0) + 1.0); // 66..86 degrees

        // Ensure latOffset + 3 stays within valid latitude range [-90, 90]
        if (latOffset + 3.0 > 90.0)
            latOffset = 85.0;

        // Construct points: 2-point leading chain, then large jump, then 3+ trailing chain
        var points = new List<GeoCoordinate>
        {
            new(0.0, 10.0),
            new(1.0, 11.0),
            new(latOffset, 12.0),
            new(latOffset + 1.0, 13.0),
            new(latOffset + 2.0, 14.0),
            new(latOffset + 3.0, 15.0),
        };

        var chains = EquirectangularProjection.SplitForMapDraw(points, width, height);

        // Expected: 2 chains — leading 2-point chain retained alongside trailing chain
        // Bug: unfixed code drops the 2-point leading chain, returning only 1 chain
        return chains.Count == 2 && chains[0].Count == 2;
    }
}
