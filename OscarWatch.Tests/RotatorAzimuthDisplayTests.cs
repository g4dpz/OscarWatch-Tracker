using OscarWatch.Core.Models;
using OscarWatch.ViewModels;

namespace OscarWatch.Tests;

public sealed class RotatorAzimuthDisplayTests
{
    [Fact]
    public void FormatRotatorAzimuthText_shows_commanded_and_compass_when_differ()
    {
        var text = MainViewModel.FormatRotatorAzimuthText(
            new RotatorPositionStatus(true, 365, 20, 370, 10));
        Assert.Equal("370° (10° sat)", text);
    }

    [Fact]
    public void FormatRotatorAzimuthText_shows_polled_when_command_matches_compass()
    {
        var text = MainViewModel.FormatRotatorAzimuthText(
            new RotatorPositionStatus(true, 350, 20, 350, 350));
        Assert.Equal("350°", text);
    }

    [Fact]
    public void FormatRotatorAzimuthText_disconnected_shows_dash()
    {
        var text = MainViewModel.FormatRotatorAzimuthText(
            new RotatorPositionStatus(false, null, null));
        Assert.Equal("—", text);
    }
}
