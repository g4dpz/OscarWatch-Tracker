using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

public sealed class DopplerDialTrackingModeTests
{
    [Fact]
    public void Non_interactive_is_automatic() =>
        Assert.Equal(DopplerDialTrackingMode.Automatic, DopplerDialTrackingMode.Resolve(
            interactive: false,
            handsOffAutomatic: false,
            dialStable: false));

    [Theory]
    [InlineData(true, true, DopplerDialTrackingMode.HandsOff)]
    [InlineData(true, false, DopplerDialTrackingMode.HandsOff)]
    [InlineData(false, false, DopplerDialTrackingMode.DialWait)]
    [InlineData(false, true, DopplerDialTrackingMode.DialTrack)]
    public void Interactive_paths(bool handsOff, bool dialStable, string expected) =>
        Assert.Equal(expected, DopplerDialTrackingMode.Resolve(
            interactive: true,
            handsOffAutomatic: handsOff,
            dialStable: dialStable));
}
