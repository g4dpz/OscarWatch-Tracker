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

    [Theory]
    [InlineData(350, 10, 450, 370)]
    [InlineData(350, 340, 450, 340)]
    [InlineData(370, 340, 450, 340)]
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

    [Fact]
    public void ResolveCommandAz_eastbound_in_overlap_unwraps_instead_of_climbing()
    {
        // Without westbound lookahead, climbing 370→20 would start the SE trap.
        Assert.Equal(20, RotatorAzimuthPlanner.ResolveCommandAz(370, 20, 450));
    }

    [Fact]
    public void ResolveCommandAz_overlap_climb_allowed_when_westbound_predicted()
    {
        Assert.Equal(380, RotatorAzimuthPlanner.ResolveCommandAz(370, 20, 450, nextCompassAzDeg: 340));
        Assert.Equal(390, RotatorAzimuthPlanner.ResolveCommandAz(380, 30, 450, nextCompassAzDeg: 330));
    }

    [Theory]
    [InlineData(350, 10, 370)]
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
    public void ResolveCommandAz_east_descent_commits_to_extended_band(
        double last,
        double target,
        double maxAz,
        double expected)
    {
        var result = RotatorAzimuthPlanner.ResolveCommandAz(last, target, maxAz);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveCommandAz_east_descent_skipped_when_lookahead_continues_southeast()
    {
        Assert.Equal(20, RotatorAzimuthPlanner.ResolveCommandAz(25, 20, 450, nextCompassAzDeg: 30));
    }

    [Fact]
    public void ResolveCommandAz_east_imminent_wrap_uses_extended_with_lookahead()
    {
        Assert.Equal(375, RotatorAzimuthPlanner.ResolveCommandAz(50, 15, 450, nextCompassAzDeg: 355));
        Assert.Equal(394, RotatorAzimuthPlanner.ResolveCommandAz(34, 34, 450, nextCompassAzDeg: 330));
    }

    [Fact]
    public void ResolveCommandAz_west_approach_with_southeast_lookahead_stays_primary()
    {
        // N→SE after crossing north: do not enter 361–450°.
        var result = RotatorAzimuthPlanner.ResolveCommandAz(350, 10, 450, nextCompassAzDeg: 20);
        Assert.Equal(10, result);
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

    [Fact]
    public void ResolveCommandAz_park_135_to_003_takes_short_path()
    {
        Assert.Equal(3, RotatorAzimuthPlanner.ResolveCommandAz(135, 3, 450));
        Assert.Equal(3, RotatorAzimuthPlanner.ResolveCommandAz(135, 3, 450, nextCompassAzDeg: 10));
    }

    [Theory]
    [InlineData(3, 15, 25, 15)]
    [InlineData(15, 45, 60, 45)]
    [InlineData(45, 90, 120, 90)]
    public void ResolveCommandAz_southeast_sequence_stays_on_primary_band(
        double last,
        double target,
        double next,
        double expected)
    {
        var result = RotatorAzimuthPlanner.ResolveCommandAz(last, target, 450, next);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveCommandAz_overlap_eastbound_climb_never_reaches_stop()
    {
        // Classic trap: entered overlap, then az increases SE toward 120.
        Assert.Equal(15, RotatorAzimuthPlanner.ResolveCommandAz(363, 15, 450, nextCompassAzDeg: 25));
        Assert.Equal(45, RotatorAzimuthPlanner.ResolveCommandAz(375, 45, 450, nextCompassAzDeg: 60));
        Assert.Equal(90, RotatorAzimuthPlanner.ResolveCommandAz(405, 90, 450, nextCompassAzDeg: 120));
        Assert.Equal(120, RotatorAzimuthPlanner.ResolveCommandAz(450, 120, 450, nextCompassAzDeg: 135));
    }

    [Fact]
    public void ResolveCommandAz_ceiling_blocks_extended_without_westbound_prediction()
    {
        // Myopic would pick 450 (90+360); ceiling forces primary when not westbound.
        Assert.Equal(90, RotatorAzimuthPlanner.ResolveCommandAz(405, 90, 450));
        Assert.True(90 + 360 <= 450);
    }

    [Theory]
    [InlineData(null, 135.0, 135.0)]
    [InlineData(135.0, null, 135.0)]
    [InlineData(370.0, 370.0, 370.0)]
    [InlineData(370.0, 135.0, 135.0)]
    [InlineData(400.0, 135.0, 135.0)]
    [InlineData(135.0, 140.0, 135.0)]
    public void ResolveEffectiveLastAzimuth_prefers_polled_when_compass_delta_large(
        double? last,
        double? polled,
        double? expected)
    {
        Assert.Equal(expected, RotatorAzimuthPlanner.ResolveEffectiveLastAzimuth(last, polled));
    }

    [Theory]
    [InlineData(350, 10, 20.0, true)]
    [InlineData(370, 20, null, true)]
    [InlineData(370, 20, 340.0, false)]
    [InlineData(25, 20, 355.0, false)]
    [InlineData(135, 3, 10.0, false)]
    public void IsEastboundSeContinuation_detects_southeast_legs(
        double last,
        double target,
        double? next,
        bool expected)
    {
        Assert.Equal(
            expected,
            RotatorAzimuthPlanner.IsEastboundSeContinuation(last, target, next));
    }
}
