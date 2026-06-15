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
    public void GetTimePattern_12_hour_uses_am_pm_not_regional_24h_default()
    {
        var culture = new CultureInfo("en-GB");
        var pattern = PassDisplayFormat.GetTimePattern(ClockDisplayFormat.TwelveHour, culture: culture);
        Assert.Contains('t', pattern);
        Assert.DoesNotContain("HH", pattern);
    }

    [Fact]
    public void FormatUtcClock_respects_12_and_24_hour()
    {
        var utc = new DateTime(2026, 6, 4, 15, 30, 45, DateTimeKind.Utc);
        var culture = new CultureInfo("en-GB");

        var twelve = PassDisplayFormat.FormatUtcClock(utc, ClockDisplayFormat.TwelveHour, culture);
        var twentyFour = PassDisplayFormat.FormatUtcClock(utc, ClockDisplayFormat.TwentyFourHour, culture);

        Assert.Contains("3:30", twelve);
        Assert.Contains("PM", twelve, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("15:30", twentyFour);
        Assert.DoesNotContain("PM", twentyFour, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormatLocalTimes_respects_12_hour_for_en_GB()
    {
        var culture = new CultureInfo("en-GB");
        var aosUtc = new DateTime(2026, 6, 4, 15, 30, 0, DateTimeKind.Utc);
        var losUtc = new DateTime(2026, 6, 4, 15, 45, 0, DateTimeKind.Utc);

        var (aos, los) = PassDisplayFormat.FormatLocalTimes(
            aosUtc,
            losUtc,
            culture,
            useUtc: true,
            clockFormat: ClockDisplayFormat.TwelveHour);

        Assert.Contains("3:30", aos);
        Assert.Contains("PM", aos, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("3:45", los);
        Assert.Contains("PM", los, StringComparison.OrdinalIgnoreCase);
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

        Assert.Contains("3:30", twelveHour);
        Assert.Contains("PM", twelveHour, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("15:30", twentyFourHour);
        Assert.Contains("15:45", twentyFourHour);
    }
}
