using System.Globalization;
using OscarWatch.Core.Display;

namespace OscarWatch.Tests;

public class PassDisplayFormatTests
{
    [Fact]
    public void GetTimePattern_24_hour_uses_HHmm()
    {
        Assert.Equal("HH:mm", PassDisplayFormat.GetTimePattern(ClockDisplayFormat.TwentyFourHour));
        Assert.Equal("HH:mm:ss", PassDisplayFormat.GetTimePattern(ClockDisplayFormat.TwentyFourHour, includeSeconds: true));
    }

    [Fact]
    public void GetTimePattern_12_hour_uses_culture_short_pattern()
    {
        var culture = new CultureInfo("en-US");
        Assert.Equal("h:mm tt", PassDisplayFormat.GetTimePattern(ClockDisplayFormat.TwelveHour, culture: culture));
    }

    [Fact]
    public void FormatUtcClock_respects_12_and_24_hour()
    {
        var utc = new DateTime(2026, 6, 4, 15, 30, 45, DateTimeKind.Utc);
        var culture = new CultureInfo("en-US");

        Assert.Contains("3:30", PassDisplayFormat.FormatUtcClock(utc, ClockDisplayFormat.TwelveHour, culture));
        Assert.Contains("15:30", PassDisplayFormat.FormatUtcClock(utc, ClockDisplayFormat.TwentyFourHour, culture));
    }
}
