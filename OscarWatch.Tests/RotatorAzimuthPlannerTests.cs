using OscarWatch.Core.Rotator;

namespace OscarWatch.Tests;

public sealed class RotatorAzimuthPlannerTests
{
    [Fact]
    public void ResolveCommandAz_without_last_command_returns_compass_target()
    {
        var result = RotatorAzimuthPlanner.ResolveCommandAz(null, 10, 450);
        Assert.Equal(10, result);
    }

    [Fact]
    public void ResolveCommandAz_aos_east_of_north_commits_to_overlap_when_path_crosses_north()
    {
        // FO-29-class: AOS ~20°, then west through 0° to LOS ~232°. First command
        // must be 380°, not 20°, or the mast sits on the primary stop.
        Assert.Equal(380, RotatorAzimuthPlanner.ResolveCommandAz(
            null, 20, 450, remainingPathCrossesNorth: true));
        Assert.Equal(380, RotatorAzimuthPlanner.ResolveCommandAz(
            180, 20, 450, remainingPathCrossesNorth: true));
    }

    [Theory]
    [InlineData(350, 10, 450, 370)]
    [InlineData(350, 340, 450, 340)]
    [InlineData(370, 340, 450, 340)]
    [InlineData(370, 20, 450, 380)]
    [InlineData(350, 10, 360, 10)]
    [InlineData(350, 340, 360, 340)]
    public void ResolveCommandAz_picks_shortest_path(
        double lastCommanded,
        double targetCompass,
        double maxAz,
        double expected)
    {
        var result = RotatorAzimuthPlanner.ResolveCommandAz(lastCommanded, targetCompass, maxAz);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(350, 10, 370)]
    [InlineData(370, 20, 380)]
    [InlineData(380, 30, 390)]
    [InlineData(390, 340, 340)]
    public void ResolveCommandAz_north_wrap_sequence(double last, double target, double expected)
    {
        var result = RotatorAzimuthPlanner.ResolveCommandAz(last, target, 450);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(-10, 350)]
    [InlineData(370, 10)]
    [InlineData(720, 0)]
    public void Normalize360_wraps_to_compass_range(double input, double expected)
    {
        Assert.Equal(expected, RotatorAzimuthPlanner.Normalize360(input));
    }

    [Theory]
    [InlineData(25, 20, 450, 380)]
    [InlineData(34, 20, 450, 380)]
    [InlineData(15, 10, 450, 370)]
    [InlineData(80, 50, 450, 50)]
    public void ResolveCommandAz_east_descent_commits_to_extended_when_pass_crosses_north(
        double last,
        double target,
        double maxAz,
        double expected)
    {
        var result = RotatorAzimuthPlanner.ResolveCommandAz(
            last, target, maxAz, remainingPathCrossesNorth: true);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(80, 50, 50)]
    [InlineData(50, 40, 40)]
    [InlineData(45, 40, 40)]
    [InlineData(34, 28, 28)]
    [InlineData(25, 20, 20)]
    public void ResolveCommandAz_northbound_without_north_crossing_stays_primary(
        double last,
        double target,
        double expected)
    {
        // RS-44 2026-08-15 IO87JP: heading north through ~40°, LOS still east of 0°.
        var result = RotatorAzimuthPlanner.ResolveCommandAz(
            last, target, 450, remainingPathCrossesNorth: false);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveCommandAz_rs44_northbound_sequence_never_uses_extended_band()
    {
        // Compact walk of the 2026-08-15 21:31 UTC RS-44 pass over IO87JP:
        // AOS ~146° SE, TCA ~79° / 30° el, then north through 45° toward LOS ~20°.
        double[] compass = [145, 112, 96, 78, 46, 40, 28, 20];
        double? last = null;
        foreach (var az in compass)
        {
            last = RotatorAzimuthPlanner.ResolveCommandAz(
                last, az, 450, remainingPathCrossesNorth: false);
            Assert.True(last <= 360, $"command {last} at compass {az} left primary band");
        }
    }

    [Fact]
    public void ResolveCommandAz_east_imminent_wrap_uses_extended_with_lookahead()
    {
        Assert.Equal(375, RotatorAzimuthPlanner.ResolveCommandAz(50, 15, 450, nextCompassAzDeg: 355));
        Assert.Equal(394, RotatorAzimuthPlanner.ResolveCommandAz(34, 34, 450, nextCompassAzDeg: 330));
    }

    [Fact]
    public void ResolveCommandAz_west_side_still_uses_myopic_shortest_path()
    {
        var result = RotatorAzimuthPlanner.ResolveCommandAz(350, 10, 450, nextCompassAzDeg: 20);
        Assert.Equal(370, result);
    }

    [Theory]
    [InlineData(10, 330, 370)]
    [InlineData(15, 330, 375)]
    [InlineData(34, 330, 394)]
    [InlineData(5, 350, 365)]
    [InlineData(25, 310, 385)]
    public void ResolveCommandAz_west_side_north_wrap_commits_to_extended(
        double last,
        double target,
        double expected)
    {
        var result = RotatorAzimuthPlanner.ResolveCommandAz(last, target, 450);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(20, 232, 18.0, true)]   // FO-29 westbound through north
    [InlineData(20, 320, null, true)] // LOS already NW: wrap is unambiguous
    [InlineData(20, 232, 25.0, false)]  // same AOS/LOS but heading east through south
    [InlineData(80, 20, 70.0, false)]   // RS-44: LOS still east of north
    [InlineData(20, 232, null, false)] // no lookahead: do not guess the long vs short sky path
    public void RemainingPathCrossesNorthEastToWest_uses_direction_when_los_is_southwest(
        double current,
        double los,
        double? next,
        bool expected)
    {
        Assert.Equal(
            expected,
            RotatorAzimuthPlanner.RemainingPathCrossesNorthEastToWest(current, los, next));
    }

    [Fact]
    public void ResolveCommandAz_fo29_westbound_sequence_stays_in_overlap_through_north()
    {
        double[] compass = [20, 12, 4, 350, 280, 232];
        double? last = null;
        double[] expected = [380, 372, 364, 350, 280, 232];
        for (var i = 0; i < compass.Length; i++)
        {
            double? next = i + 1 < compass.Length ? compass[i + 1] : null;
            var crosses = RotatorAzimuthPlanner.RemainingPathCrossesNorthEastToWest(
                compass[i], 232, next);
            last = RotatorAzimuthPlanner.ResolveCommandAz(last, compass[i], 450, next, crosses);
            Assert.Equal(expected[i], last);
        }
    }

    [Theory]
    [InlineData(10, 330, true)]
    [InlineData(25, 310, true)]
    [InlineData(34, 330, true)]
    [InlineData(89, 330, true)]
    [InlineData(95, 330, false)]
    [InlineData(10, 260, false)]
    public void ShouldCommitWestSideNorthWrap_detects_east_to_west_jump(
        double last,
        double target,
        bool expected)
    {
        Assert.Equal(
            expected,
            RotatorAzimuthPlanner.ShouldCommitWestSideNorthWrap(target, last, 450));
    }

    [Theory]
    [InlineData(15, 355, true)]
    [InlineData(34, 330, true)]
    [InlineData(45, 355, false)]
    [InlineData(80, 50, false)]
    [InlineData(10, 200, false)]
    public void ShouldUseExtendedForImminentEastWrap_detects_east_to_west_jump(
        double target,
        double next,
        bool expected)
    {
        Assert.Equal(
            expected,
            RotatorAzimuthPlanner.ShouldUseExtendedForImminentEastWrap(target, next, 450));
    }

    [Theory]
    [InlineData(40, 40, 1.0, true)]
    [InlineData(40, 70, 1.0, false)]
    [InlineData(15, 375, 1.0, true)]
    [InlineData(375, 15, 1.0, true)]
    [InlineData(0, 359, 1.0, false)]
    [InlineData(0, 359.6, 1.0, true)]
    [InlineData(400, 40, 1.0, true)]
    public void IsWithinAzimuthThreshold_treats_overlap_and_compass_wrap_as_same_heading(
        double first,
        double second,
        double threshold,
        bool expected)
    {
        Assert.Equal(expected, RotatorAzimuthPlanner.IsWithinAzimuthThreshold(first, second, threshold));
    }
}
