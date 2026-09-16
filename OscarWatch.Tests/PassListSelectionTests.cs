using OscarWatch.Core.Display;
using OscarWatch.Core.Models;
using OscarWatch.ViewModels;

namespace OscarWatch.Tests;

public sealed class PassListSelectionTests
{
    private static readonly DateTime Base = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FindMatchingRow_keeps_later_pass_of_the_same_satellite()
    {
        var first = MakePass("25544", Base);
        var later = MakePass("25544", Base.AddHours(2));
        var other = MakePass("99999", Base.AddMinutes(30));
        IPassListItem[] items = [first, other, later];

        var match = PassListSelection.FindMatchingRow(items, later.NoradId, later.AosUtc);

        Assert.Same(later, match);
    }

    [Fact]
    public void FindMatchingRow_falls_back_to_first_pass_when_aos_no_longer_listed()
    {
        var first = MakePass("25544", Base);
        var other = MakePass("99999", Base.AddMinutes(30));
        IPassListItem[] items = [first, other];

        var match = PassListSelection.FindMatchingRow(items, "25544", Base.AddHours(2));

        Assert.Same(first, match);
    }

    [Fact]
    public void IsRowForSatellite_is_true_for_a_later_pass_of_the_focused_bird()
    {
        var later = MakePass("25544", Base.AddHours(2));

        Assert.True(PassListSelection.IsRowForSatellite(later, "25544"));
        Assert.False(PassListSelection.IsRowForSatellite(later, "99999"));
        Assert.False(PassListSelection.IsRowForSatellite(new PassDayHeaderViewModel { DateLabel = "Today" }, "25544"));
    }

    [Fact]
    public void FindMatchingRow_matches_aos_after_pass_utc_normalise()
    {
        var listed = MakePass("25544", Base);
        IPassListItem[] items = [listed];
        var unspecifiedAos = DateTime.SpecifyKind(Base, DateTimeKind.Unspecified);

        var match = PassListSelection.FindMatchingRow(items, "25544", unspecifiedAos);

        Assert.Same(listed, match);
        Assert.Equal(PassUtc.Normalize(Base), PassUtc.Normalize(unspecifiedAos));
    }

    private static PassRowViewModel MakePass(string noradId, DateTime aosUtc) => new()
    {
        Source = new PassInfo
        {
            SatelliteName = noradId,
            NoradId = noradId,
            AosUtc = aosUtc,
            LosUtc = aosUtc.AddMinutes(15)
        },
        SatelliteName = noradId,
        NoradId = noradId,
        AosUtc = aosUtc,
        LosUtc = aosUtc.AddMinutes(15),
        MaxElevationUtc = aosUtc.AddMinutes(7)
    };
}
