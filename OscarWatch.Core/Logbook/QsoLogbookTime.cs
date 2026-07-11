using System.Globalization;
using OscarWatch.Core.Display;

namespace OscarWatch.Core.Logbook;

public static class QsoLogbookTime
{
    public static DateTime NormalizeToUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    public static string FormatQsoUtc(DateTime utc, bool use24HourClock, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;
        var instant = NormalizeToUtc(utc);
        var clockFormat = PassDisplayFormat.FromSettings(use24HourClock);
        var datePattern = culture.DateTimeFormat.ShortDatePattern;
        var timePattern = PassDisplayFormat.GetTimePattern(clockFormat, culture: culture);
        return instant.ToString($"{datePattern} {timePattern}", culture);
    }

    public static string FormatLiveUtcClock(DateTime utc, bool use24HourClock, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;
        var instant = NormalizeToUtc(utc);
        var clockFormat = PassDisplayFormat.FromSettings(use24HourClock);
        var datePattern = culture.DateTimeFormat.ShortDatePattern;
        var timePattern = PassDisplayFormat.GetTimePattern(clockFormat, includeSeconds: true, culture);
        return $"{instant.ToString($"{datePattern} {timePattern}", culture)} UTC";
    }
}
