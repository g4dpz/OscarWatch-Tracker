using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

public sealed class MainWindowLayoutDefaultsTests
{
    [Fact]
    public void TryGetSavedSize_rejects_invalid_dimensions()
    {
        var settings = new AppSettings { MainWindowWidth = 100, MainWindowHeight = 556 };
        Assert.False(MainWindowLayoutDefaults.TryGetSavedSize(settings, out _, out _));

        settings.MainWindowWidth = 1280;
        settings.MainWindowHeight = 200;
        Assert.False(MainWindowLayoutDefaults.TryGetSavedSize(settings, out _, out _));

        settings.MainWindowHeight = 556;
        Assert.True(MainWindowLayoutDefaults.TryGetSavedSize(settings, out var width, out var height));
        Assert.Equal(1280, width);
        Assert.Equal(556, height);
    }

    [Fact]
    public void TryGetSavedPosition_requires_both_coordinates()
    {
        var settings = new AppSettings { MainWindowX = 120 };
        Assert.False(MainWindowLayoutDefaults.TryGetSavedPosition(settings, out _, out _));

        settings.MainWindowY = 80;
        Assert.True(MainWindowLayoutDefaults.TryGetSavedPosition(settings, out var x, out var y));
        Assert.Equal(120, x);
        Assert.Equal(80, y);
    }
}
