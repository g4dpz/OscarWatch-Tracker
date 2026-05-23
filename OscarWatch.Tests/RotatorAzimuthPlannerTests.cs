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
    public void ResolveCommandAz_east_imminent_wrap_uses_extended_with_lookahead()
    {
        var result = RotatorAzimuthPlanner.ResolveCommandAz(50, 15, 450, nextCompassAzDeg: 355);
        Assert.Equal(375, result);
    }

    [Fact]
    public void ResolveCommandAz_west_side_still_uses_myopic_shortest_path()
    {
        var result = RotatorAzimuthPlanner.ResolveCommandAz(350, 10, 450, nextCompassAzDeg: 20);
        Assert.Equal(370, result);
    }

    [Theory]
    [InlineData(15, 355, true)]
    [InlineData(25, 355, false)]
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
}
