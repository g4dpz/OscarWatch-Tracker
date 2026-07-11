using OscarWatch.Core.Models;
using OscarWatch.ViewModels;

namespace OscarWatch.Tests;

public class SkyPlotPassSelectionTests
{
    private static PassInfo MakePass(string noradId, DateTime aos, DateTime los) => new()
    {
        SatelliteName = "TEST",
        NoradId = noradId,
        AosUtc = aos,
        LosUtc = los,
        MaxElevationDeg = 45,
        MaxElevationUtc = aos.AddMinutes(5),
        AosAzimuthDeg = 10,
        LosAzimuthDeg = 350
    };

    [Fact]
    public void FindSkyPlotPass_prefers_in_progress_pass()
    {
        var now = new DateTime(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc);
        var passes = new[]
        {
            MakePass("25544", now.AddMinutes(-5), now.AddMinutes(10)),
            MakePass("25544", now.AddHours(2), now.AddHours(2).AddMinutes(12))
        };

        var selected = MainViewModel.FindSkyPlotPass("25544", passes, now);

        Assert.NotNull(selected);
        Assert.Equal(passes[0].AosUtc, selected.AosUtc);
    }

    [Fact]
    public void FindSkyPlotPass_returns_next_upcoming_when_not_in_progress()
    {
        var now = new DateTime(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc);
        var passes = new[]
        {
            MakePass("25544", now.AddHours(1), now.AddHours(1).AddMinutes(12)),
            MakePass("25544", now.AddHours(3), now.AddHours(3).AddMinutes(12))
        };

        var selected = MainViewModel.FindSkyPlotPass("25544", passes, now);

        Assert.NotNull(selected);
        Assert.Equal(passes[0].AosUtc, selected.AosUtc);
    }

    [Fact]
    public void FindSkyPlotPass_returns_null_when_no_matching_pass()
    {
        var now = DateTime.UtcNow;
        var passes = new[] { MakePass("25544", now.AddHours(1), now.AddHours(1).AddMinutes(10)) };

        Assert.Null(MainViewModel.FindSkyPlotPass("99999", passes, now));
    }
}
