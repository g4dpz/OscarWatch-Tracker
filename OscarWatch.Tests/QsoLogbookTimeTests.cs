using System.Globalization;
using OscarWatch.Core.Logbook;

namespace OscarWatch.Tests;

public sealed class QsoLogbookTimeTests
{
    private static readonly CultureInfo EnGb = CultureInfo.GetCultureInfo("en-GB");

    [Fact]
    public void FormatQsoUtc_always_uses_utc_not_local()
    {
        var utc = new DateTime(2020, 2, 12, 17, 10, 0, DateTimeKind.Utc);
        var formatted = QsoLogbookTime.FormatQsoUtc(utc, use24HourClock: true, EnGb);
        Assert.Equal("12/02/2020 17:10", formatted);
    }

    [Fact]
    public void FormatQsoUtc_uses_regional_date_order()
    {
        var utc = new DateTime(2020, 2, 12, 17, 10, 0, DateTimeKind.Utc);
        var culture = CultureInfo.GetCultureInfo("en-US");
        var formatted = QsoLogbookTime.FormatQsoUtc(utc, use24HourClock: true, culture);
        Assert.Equal("2/12/2020 17:10", formatted);
    }

    [Fact]
    public void NormalizeToUtc_converts_local_to_utc()
    {
        var local = new DateTime(2020, 2, 12, 17, 10, 0, DateTimeKind.Local);
        var normalized = QsoLogbookTime.NormalizeToUtc(local);
        Assert.Equal(DateTimeKind.Utc, normalized.Kind);
        Assert.Equal(local.ToUniversalTime(), normalized);
    }

    [Fact]
    public void FormatLiveUtcClock_uses_regional_format_and_utc_suffix()
    {
        var utc = new DateTime(2020, 2, 12, 17, 10, 5, DateTimeKind.Utc);
        Assert.Equal("12/02/2020 17:10:05 UTC", QsoLogbookTime.FormatLiveUtcClock(utc, use24HourClock: true, EnGb));
    }

    [Fact]
    public void FormatLiveUtcClock_respects_12_hour_setting()
    {
        var utc = new DateTime(2020, 2, 12, 17, 10, 5, DateTimeKind.Utc);
        var formatted = QsoLogbookTime.FormatLiveUtcClock(utc, use24HourClock: false, EnGb);
        Assert.Contains("5:10", formatted);
        Assert.Contains("PM", formatted, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("UTC", formatted);
    }
}
