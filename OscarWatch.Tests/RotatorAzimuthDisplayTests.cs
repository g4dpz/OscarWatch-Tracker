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

    [Fact]
    public void FormatRotatorAzimuthText_falls_back_to_commanded_when_no_poll()
    {
        var text = MainViewModel.FormatRotatorAzimuthText(
            new RotatorPositionStatus(true, null, null, CommandedAzimuthDeg: 250, CompassAzimuthDeg: 250));
        Assert.Equal("250°", text);
    }

    [Fact]
    public void FormatRotatorElevationText_falls_back_to_commanded_when_no_poll()
    {
        var text = MainViewModel.FormatRotatorElevationText(
            new RotatorPositionStatus(true, null, null, CommandedElevationDeg: 6));
        Assert.Equal("6°", text);
    }

    [Fact]
    public void FormatRotatorElevationText_prefers_polled_over_commanded()
    {
        var text = MainViewModel.FormatRotatorElevationText(
            new RotatorPositionStatus(true, null, 12, CommandedElevationDeg: 6));
        Assert.Equal("12°", text);
    }

    [Fact]
    public void FormatRotatorElevationText_disconnected_shows_dash()
    {
        var text = MainViewModel.FormatRotatorElevationText(
            new RotatorPositionStatus(false, null, null, CommandedElevationDeg: 6));
        Assert.Equal("—", text);
    }
}
