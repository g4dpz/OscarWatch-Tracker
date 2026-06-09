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
        var culture = new CultureInfo("en-GB");
        Assert.Equal("HH:mm", PassDisplayFormat.GetTimePattern(ClockDisplayFormat.TwelveHour, culture: culture));
    }

    [Fact]
    public void FormatUtcClock_respects_12_and_24_hour()
    {
        var utc = new DateTime(2026, 6, 4, 15, 30, 45, DateTimeKind.Utc);
        var culture = new CultureInfo("en-GB");

        Assert.Contains("15:30", PassDisplayFormat.FormatUtcClock(utc, ClockDisplayFormat.TwelveHour, culture));
        Assert.Contains("15:30", PassDisplayFormat.FormatUtcClock(utc, ClockDisplayFormat.TwentyFourHour, culture));
    }

    [Fact]
    public void FormatCountdownHms_uses_total_hours_not_wrapped_component()
    {
        Assert.Equal("0:00:00", PassDisplayFormat.FormatCountdownHms(TimeSpan.Zero));
        Assert.Equal("1:30:45", PassDisplayFormat.FormatCountdownHms(TimeSpan.FromSeconds(5445)));
        Assert.Equal("36:00:00", PassDisplayFormat.FormatCountdownHms(TimeSpan.FromHours(36)));
    }

    [Fact]
    public void FormatAlertWindow_respects_12_and_24_hour()
    {
        var aosUtc = new DateTime(2026, 6, 4, 15, 30, 0, DateTimeKind.Utc);
        var losUtc = new DateTime(2026, 6, 4, 15, 45, 0, DateTimeKind.Utc);
        var culture = new CultureInfo("en-GB");

        var twelveHour = HamsAtDisplayFormat.FormatAlertWindow(
            aosUtc, losUtc, useUtc: true, ClockDisplayFormat.TwelveHour, culture);
        var twentyFourHour = HamsAtDisplayFormat.FormatAlertWindow(
            aosUtc, losUtc, useUtc: true, ClockDisplayFormat.TwentyFourHour, culture);

        Assert.Contains("15:30", twelveHour);
        Assert.Contains("15:45", twelveHour);
        Assert.Contains("15:30", twentyFourHour);
        Assert.Contains("15:45", twentyFourHour);
    }
}
