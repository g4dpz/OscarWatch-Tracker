using OscarWatch.Core.Radio;

namespace OscarWatch.Tests;

public class DopplerAdaptiveThresholdTests
{
    [Theory]
    [InlineData(50, 10, 50)]
    [InlineData(50, 15, 50)]
    [InlineData(50, 25, 38)]
    [InlineData(50, 35, 25)]
    [InlineData(50, 50, 25)]
    [InlineData(50, 30, 50, false)]
    public void Resolve_scales_threshold_with_slew_rate(int baseline, double slewHzPerSec, int expected, bool enabled = true)
    {
        Assert.Equal(expected, DopplerAdaptiveThreshold.Resolve(baseline, slewHzPerSec, enabled));
    }

    [Fact]
    public void Slew_from_slope_matches_downlink_physics_order_of_magnitude()
    {
        // RS-44-class steep leg: ~0.016 km/s² at 435 MHz → ~23 Hz/s
        var slew = DopplerAdaptiveThreshold.SlewFromRangeRateSlope(435_667, 0.016);
        Assert.InRange(slew, 18, 28);
    }
}
