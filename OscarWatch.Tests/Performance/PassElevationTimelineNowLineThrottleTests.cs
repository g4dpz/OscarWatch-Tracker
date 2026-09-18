using OscarWatch.Controls;
using Xunit;

namespace OscarWatch.Tests.Performance;

/// <summary>
/// Pass elevation timeline redraws only when window/live time drift reaches ≥ 1 px.
/// </summary>
public sealed class PassElevationTimelineNowLineThrottleTests
{
    [Fact]
    public void Sub_pixel_time_drift_does_not_invalidate()
    {
        // 800 px plot, 120 min window → 0.15 min per pixel
        var windowStart = new DateTime(2026, 9, 8, 16, 0, 0, DateTimeKind.Utc);
        var live = windowStart;
        var lastWindow = windowStart;
        var lastLive = live;

        var drifted = windowStart.AddSeconds(5); // 5 s = 0.083 min < 0.15
        Assert.False(PassElevationTimelineControl.HasTimeAdvancedBeyondThreshold(
            drifted, drifted, lastWindow, lastLive, plotWidth: 800, windowMinutes: 120));
    }

    [Fact]
    public void One_pixel_window_drift_invalidates()
    {
        var windowStart = new DateTime(2026, 9, 8, 16, 0, 0, DateTimeKind.Utc);
        var live = windowStart;
        // 0.15 min = 9 s for 1 px at 800/120
        var drifted = windowStart.AddSeconds(9);

        Assert.True(PassElevationTimelineControl.HasTimeAdvancedBeyondThreshold(
            drifted, live.AddSeconds(9), windowStart, live, plotWidth: 800, windowMinutes: 120));
    }

    [Fact]
    public void First_paint_always_invalidates()
    {
        var now = DateTime.UtcNow;
        Assert.True(PassElevationTimelineControl.HasTimeAdvancedBeyondThreshold(
            now, now, DateTime.MinValue, DateTime.MinValue, plotWidth: 800, windowMinutes: 120));
    }

    [Fact]
    public void Zero_plot_width_invalidates()
    {
        var now = DateTime.UtcNow;
        Assert.True(PassElevationTimelineControl.HasTimeAdvancedBeyondThreshold(
            now, now, now, now, plotWidth: 0, windowMinutes: 120));
    }

    [Fact]
    public void Live_now_alone_can_invalidate_when_window_fixed()
    {
        var windowStart = new DateTime(2026, 9, 8, 16, 0, 0, DateTimeKind.Utc);
        var lastLive = windowStart.AddMinutes(-15);
        var newLive = lastLive.AddSeconds(10);

        Assert.True(PassElevationTimelineControl.HasTimeAdvancedBeyondThreshold(
            windowStart, newLive, windowStart, lastLive, plotWidth: 800, windowMinutes: 120));
    }
}
