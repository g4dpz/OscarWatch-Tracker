using OscarWatch.Core.Geo;

namespace OscarWatch.Tests;

public sealed class DayNightTerminatorTests
{
    [Fact]
    public void Subsolar_longitude_advances_with_utc()
    {
        var day = new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc);
        var a = DayNightTerminator.GetSubsolarPoint(day);
        var b = DayNightTerminator.GetSubsolarPoint(day.AddHours(1));
        var delta = Math.Abs(b.LongitudeDeg - a.LongitudeDeg);
        if (delta > 180)
            delta = 360 - delta;

        Assert.InRange(delta, 10, 20);
    }

    [Fact]
    public void London_midday_june_is_daylight()
    {
        var utc = new DateTime(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(DayNightTerminator.IsSunAboveHorizon(51.5, -0.1, utc));
    }

    [Fact]
    public void Terminator_ring_has_valid_latitudes()
    {
        var utc = new DateTime(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);
        var geometry = DayNightTerminator.GetGeometry(utc);
        Assert.True(geometry.Terminator.Count >= 200);
        foreach (var p in geometry.Terminator)
        {
            Assert.InRange(p.LatitudeDeg, -90, 90);
            Assert.InRange(p.LongitudeDeg, -180, 180);
        }
    }

    [Fact]
    public void June_morning_terminator_lies_in_southern_hemisphere()
    {
        var utc = new DateTime(2026, 6, 4, 9, 28, 0, DateTimeKind.Utc);
        var geometry = DayNightTerminator.GetGeometry(utc);
        Assert.True(geometry.NightTowardSouth);
        Assert.True(geometry.SubsolarLatitudeDeg > 10);
        var lon0 = DayNightTerminator.GetTerminatorLatitudeDeg(0, utc);
        Assert.True(lon0 < 0, $"Expected southern terminator lat, got {lon0}");
    }

    [Fact]
    public void Geometry_is_cached_per_utc_minute()
    {
        var utc = new DateTime(2026, 6, 21, 12, 0, 30, DateTimeKind.Utc);
        var a = DayNightTerminator.GetGeometry(utc);
        var b = DayNightTerminator.GetGeometry(utc.AddSeconds(20));
        Assert.Same(a, b);
    }
}
