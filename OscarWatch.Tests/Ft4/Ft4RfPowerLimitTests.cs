using OscarWatch.Core.Ft4;
using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;

namespace OscarWatch.Tests.Ft4;

public sealed class Ft4RfPowerLimitTests
{
    [Theory]
    [InlineData(30.0, false)]
    [InlineData(30.01, true)]
    [InlineData(50.0, true)]
    [InlineData(10.0, false)]
    public void ExceedsLimit_uses_strictly_greater_than_30(double watts, bool expected) =>
        Assert.Equal(expected, Ft4RfPowerLimit.ExceedsLimit(watts));
}

public sealed class IcomRfPowerEstimatorTests
{
    [Fact]
    public void Ic9700_2m_full_scale_is_100_watts()
    {
        Assert.True(IcomRfPowerEstimator.TryEstimateWatts(
            RigType.IcomIc9700, 145_900_000, 255, out var watts));
        Assert.Equal(100.0, watts, precision: 3);
    }

    [Fact]
    public void Ic9700_70cm_level_that_exceeds_30w()
    {
        // 30 W on 75 W max => 30/75 * 255 = 102
        Assert.True(IcomRfPowerEstimator.TryEstimateWatts(
            RigType.IcomIc9700, 435_800_000, 120, out var watts));
        Assert.True(Ft4RfPowerLimit.ExceedsLimit(watts));
    }

    [Fact]
    public void Ic9700_70cm_level_under_30w()
    {
        Assert.True(IcomRfPowerEstimator.TryEstimateWatts(
            RigType.IcomIc9700, 435_800_000, 80, out var watts));
        Assert.False(Ft4RfPowerLimit.ExceedsLimit(watts));
    }

    [Fact]
    public void Ic9700_23cm_max_is_10w_never_exceeds_ft4_limit()
    {
        Assert.True(IcomRfPowerEstimator.TryEstimateWatts(
            RigType.IcomIc9700, 1_269_500_000, 255, out var watts));
        Assert.Equal(10.0, watts, precision: 3);
        Assert.False(Ft4RfPowerLimit.ExceedsLimit(watts));
    }

    [Fact]
    public void Unsupported_rig_returns_false()
    {
        Assert.False(IcomRfPowerEstimator.TryEstimateWatts(
            RigType.YaesuFt991a, 145_900_000, 200, out _));
    }
}
