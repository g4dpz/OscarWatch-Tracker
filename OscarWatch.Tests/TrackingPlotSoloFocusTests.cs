using OscarWatch.Controls;

namespace OscarWatch.Tests;

public class TrackingPlotSoloFocusTests
{
    [Theory]
    [InlineData(false, null, "25544", true)]
    [InlineData(false, "25544", "99999", true)]
    [InlineData(true, "25544", "25544", true)]
    [InlineData(true, "25544", "99999", false)]
    [InlineData(true, null, "25544", false)]
    [InlineData(true, "", "25544", false)]
    public void IsPlotSatelliteVisible_respects_solo_focus(bool solo, string? focused, string norad, bool expected) =>
        Assert.Equal(expected, TrackingPlotAccessibility.IsPlotSatelliteVisible(solo, focused, norad));
}
