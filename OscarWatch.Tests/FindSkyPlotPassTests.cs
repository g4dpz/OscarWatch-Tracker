using OscarWatch.Core.Models;
using OscarWatch.ViewModels;

namespace OscarWatch.Tests;

public sealed class FindSkyPlotPassTests
{
    [Fact]
    public void Prefers_in_progress_pass_for_focused_norad()
    {
        var now = new DateTime(2026, 8, 8, 12, 5, 0, DateTimeKind.Utc);
        var earlier = MakePass("1", now.AddMinutes(-20), now.AddMinutes(-5));
        var inProgress = MakePass("1", now.AddMinutes(-2), now.AddMinutes(8));
        var next = MakePass("1", now.AddMinutes(30), now.AddMinutes(40));

        var result = MainViewModel.FindSkyPlotPass("1", [earlier, inProgress, next], now);
        Assert.Same(inProgress, result);
    }

    [Fact]
    public void Falls_back_to_next_upcoming_when_not_in_progress()
    {
        var now = new DateTime(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);
        var next = MakePass("1", now.AddMinutes(10), now.AddMinutes(20));
        var later = MakePass("1", now.AddMinutes(60), now.AddMinutes(70));

        var result = MainViewModel.FindSkyPlotPass("1", [next, later], now);
        Assert.Same(next, result);
    }

    [Fact]
    public void Returns_null_when_norad_has_no_future_pass()
    {
        var now = new DateTime(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);
        var past = MakePass("1", now.AddMinutes(-30), now.AddMinutes(-10));
        Assert.Null(MainViewModel.FindSkyPlotPass("1", [past], now));
    }

    private static PassInfo MakePass(string noradId, DateTime aos, DateTime los) => new()
    {
        SatelliteName = "TEST",
        NoradId = noradId,
        AosUtc = aos,
        LosUtc = los,
        MaxElevationDeg = 30,
        MaxElevationUtc = aos.AddMinutes(5),
        AosAzimuthDeg = 10,
        LosAzimuthDeg = 200
    };
}
