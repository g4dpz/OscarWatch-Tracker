using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

public sealed class TimelineDetachVisibilityTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public void Docked_visible_only_when_expanded_and_not_detached(
        bool expanded,
        bool detached,
        bool expectedDockedVisible)
    {
        Assert.Equal(expectedDockedVisible, expanded && !detached);
    }

    [Fact]
    public void TimelineDetachedWindowDefaults_rejects_undersized_saved_bounds()
    {
        var settings = new AppSettings
        {
            TimelineDetachedWindowWidth = 100,
            TimelineDetachedWindowHeight = 50,
            TimelineDetachedWindowX = 10,
            TimelineDetachedWindowY = 20
        };

        Assert.False(TimelineDetachedWindowDefaults.TryGetSavedSize(settings, out _, out _));
        Assert.True(TimelineDetachedWindowDefaults.TryGetSavedPosition(settings, out var x, out var y));
        Assert.Equal(10, x);
        Assert.Equal(20, y);
    }

    [Fact]
    public void TimelineDetachedWindowDefaults_accepts_valid_saved_size()
    {
        var settings = new AppSettings
        {
            TimelineDetachedWindowWidth = TimelineDetachedWindowDefaults.MinWidth,
            TimelineDetachedWindowHeight = TimelineDetachedWindowDefaults.MinHeight
        };

        Assert.True(TimelineDetachedWindowDefaults.TryGetSavedSize(settings, out var width, out var height));
        Assert.Equal(TimelineDetachedWindowDefaults.MinWidth, width);
        Assert.Equal(TimelineDetachedWindowDefaults.MinHeight, height);
    }

    [Fact]
    public void AppSettings_timeline_detach_defaults()
    {
        var settings = new AppSettings();
        Assert.True(settings.IsTimelineExpanded);
        Assert.False(settings.IsTimelineDetached);
        Assert.Null(settings.TimelineDetachedWindowWidth);
        Assert.Null(settings.TimelineDetachedWindowHeight);
        Assert.Null(settings.TimelineDetachedWindowX);
        Assert.Null(settings.TimelineDetachedWindowY);
    }
}
