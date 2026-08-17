using FsCheck.Xunit;
using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

public sealed class TimelineWindowLimitsTests
{
    [Fact]
    public void Zoom_shortens_along_steps()
    {
        Assert.Equal(90, TimelineWindowLimits.Zoom(120, zoomInDirection: 1));
        Assert.Equal(60, TimelineWindowLimits.Zoom(90, zoomInDirection: 1));
        Assert.Equal(30, TimelineWindowLimits.Zoom(45, zoomInDirection: 1));
        Assert.Equal(30, TimelineWindowLimits.Zoom(30, zoomInDirection: 1));
    }

    [Fact]
    public void Zoom_lengthens_along_steps()
    {
        Assert.Equal(45, TimelineWindowLimits.Zoom(30, zoomInDirection: -1));
        Assert.Equal(180, TimelineWindowLimits.Zoom(120, zoomInDirection: -1));
        Assert.Equal(360, TimelineWindowLimits.Zoom(240, zoomInDirection: -1));
        Assert.Equal(360, TimelineWindowLimits.Zoom(360, zoomInDirection: -1));
    }

    [Fact]
    public void Zoom_snaps_off_step_values_in_the_requested_direction()
    {
        Assert.Equal(60, TimelineWindowLimits.Zoom(75, zoomInDirection: 1));
        Assert.Equal(90, TimelineWindowLimits.Zoom(75, zoomInDirection: -1));
    }

    [Fact]
    public void Zoom_zero_direction_clamps_only()
    {
        Assert.Equal(75, TimelineWindowLimits.Zoom(75, zoomInDirection: 0));
        Assert.Equal(30, TimelineWindowLimits.Zoom(10, zoomInDirection: 0));
        Assert.Equal(360, TimelineWindowLimits.Zoom(999, zoomInDirection: 0));
    }

    [Property]
    public bool Zoom_stays_within_limits(int currentMinutes, int direction)
    {
        var next = TimelineWindowLimits.Zoom(currentMinutes, direction);
        return next >= TimelineWindowLimits.MinMinutes && next <= TimelineWindowLimits.MaxMinutes;
    }

    [Property]
    public bool Zoom_in_never_lengthens(int currentMinutes)
    {
        var current = TimelineWindowLimits.Clamp(currentMinutes);
        return TimelineWindowLimits.Zoom(current, 1) <= current;
    }

    [Property]
    public bool Zoom_out_never_shortens(int currentMinutes)
    {
        var current = TimelineWindowLimits.Clamp(currentMinutes);
        return TimelineWindowLimits.Zoom(current, -1) >= current;
    }

    [Fact]
    public void Clamp_rejects_out_of_range()
    {
        Assert.Equal(30, TimelineWindowLimits.Clamp(0));
        Assert.Equal(360, TimelineWindowLimits.Clamp(10_000));
        Assert.Equal(120, TimelineWindowLimits.Clamp(120));
    }
}
