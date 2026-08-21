using OscarWatch.Core.Display;
using OscarWatch.Core.Models;
using OscarWatch.ViewModels;

namespace OscarWatch.Tests;

public sealed class HamsAtRoveRowViewModelTests
{
    [Fact]
    public void From_accepts_duplicate_grids_from_hams_at_and_cloudlog()
    {
        var alert = new HamsAtUpcomingAlert
        {
            Callsign = "EA1FWI",
            Grids = ["IN61", "IN61"],
            AosUtc = new DateTime(2026, 8, 26, 11, 14, 20, DateTimeKind.Utc),
            LosUtc = new DateTime(2026, 8, 26, 11, 31, 25, DateTimeKind.Utc),
            Satellite = new HamsAtSatelliteInfo { Name = "RS-44", Number = 44909 }
        };
        CloudlogGridCheckResult[] checks =
        [
            new() { Grid = "IN61", IsWorked = false },
            new() { Grid = "IN61", IsWorked = false }
        ];

        var row = HamsAtRoveRowViewModel.From(
            alert,
            useUtc: true,
            ClockDisplayFormat.TwentyFourHour,
            checks);

        Assert.Equal("EA1FWI", row.Callsign);
        Assert.Equal("IN61", row.GridsText);
        Assert.Equal("IN61", row.NeededGridsText);
        Assert.Equal("", row.WorkedGridsText);
    }

    [Fact]
    public void UniqueGrids_keeps_first_casing_and_drops_blanks()
    {
        var unique = HamsAtRoveRowViewModel.UniqueGrids(["IN61", "in61", " ", "IN62"]).ToArray();
        Assert.Equal(["IN61", "IN62"], unique);
    }
}
