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

    [Fact]
    public void GridSquareForStatus_requires_auto_update_and_non_empty_grid()
    {
        var autoUpdate = new GpsSettings { Enabled = true, AutoUpdateStation = true };
        Assert.Equal("IO87JP", GpsStatusHelper.GridSquareForStatus(autoUpdate, "io87jp"));
        Assert.Null(GpsStatusHelper.GridSquareForStatus(autoUpdate, ""));
        Assert.Null(GpsStatusHelper.GridSquareForStatus(autoUpdate, "   "));

        var gpsOnly = new GpsSettings { Enabled = true };
        Assert.Null(GpsStatusHelper.GridSquareForStatus(gpsOnly, "IO87JP"));

        var disabled = new GpsSettings { AutoUpdateStation = true };
        Assert.Null(GpsStatusHelper.GridSquareForStatus(disabled, "IO87JP"));
    }
}
