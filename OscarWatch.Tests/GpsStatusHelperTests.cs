using OscarWatch.Core.Hardware;
using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

public sealed class GpsStatusHelperTests
{
    [Fact]
    public void ShowGpsIndicator_only_when_enabled()
    {
        Assert.False(GpsStatusHelper.ShowGpsIndicator(new GpsSettings()));
        Assert.True(GpsStatusHelper.ShowGpsIndicator(new GpsSettings { Enabled = true }));
    }

    [Fact]
    public void ShowGpsTimeIndicator_requires_enabled_and_time_sync()
    {
        var disabled = new GpsSettings { UseGpsTimeForTracking = true };
        Assert.False(GpsStatusHelper.ShowGpsTimeIndicator(disabled));

        var enabledNoTime = new GpsSettings { Enabled = true };
        Assert.False(GpsStatusHelper.ShowGpsTimeIndicator(enabledNoTime));

        var both = new GpsSettings { Enabled = true, UseGpsTimeForTracking = true };
        Assert.True(GpsStatusHelper.ShowGpsTimeIndicator(both));
    }

    [Fact]
    public void IsGpsTimeActive_requires_recent_tracking_utc()
    {
        var settings = new GpsSettings { Enabled = true, UseGpsTimeForTracking = true };
        Assert.False(GpsStatusHelper.IsGpsTimeActive(settings, null));
        Assert.True(GpsStatusHelper.IsGpsTimeActive(settings, DateTime.UtcNow));
    }
}
