using System.Globalization;

namespace OscarWatch.Core.Display;

public static class PassDisplayFormat
{
    public static bool Use24Hour(ClockDisplayFormat format) =>
        format == ClockDisplayFormat.TwentyFourHour;

    public static string GetTimePattern(
        ClockDisplayFormat format,
        bool includeSeconds = false,
        CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;
        if (Use24Hour(format))
            return includeSeconds ? "HH:mm:ss" : "HH:mm";

        return GetTwelveHourTimePattern(culture, includeSeconds);
    }

    /// <summary>
    /// App 12-hour mode always includes an AM/PM designator. Regional <see cref="DateTimeFormatInfo.ShortTimePattern"/>
    /// (e.g. en-GB <c>HH:mm</c>) follows locale defaults and is not used here.
    /// </summary>
    private static string GetTwelveHourTimePattern(CultureInfo culture, bool includeSeconds)
    {
        var patterns = culture.DateTimeFormat.GetAllDateTimePatterns(includeSeconds ? 'T' : 't');
        foreach (var pattern in patterns)
        {
            var normalized = NormalizeTimePattern(pattern);
            if (normalized.Contains('t', StringComparison.Ordinal))
                return normalized;
        }

        return includeSeconds ? "h:mm:ss tt" : "h:mm tt";
    }

    /// <summary>ICU/Linux cultures may use narrow no-break space (U+202F) before AM/PM; normalise for stable display and tests.</summary>
    private static string NormalizeTimePattern(string pattern) =>
        pattern.Replace('\u202F', ' ').Replace('\u00A0', ' ');

    public static (string Aos, string Los) FormatAosLos(
        DateTime aosUtc,
        DateTime losUtc,
        ClockDisplayFormat clockFormat = ClockDisplayFormat.TwelveHour,
        CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;
        var datePattern = culture.DateTimeFormat.ShortDatePattern;
        var timePattern = GetTimePattern(clockFormat, culture: culture);

        var aos = ToLocal(aosUtc);
        var los = ToLocal(losUtc);
        var aosText = aos.ToString($"{datePattern} {timePattern}", culture);
        var losText = aos.Date == los.Date
            ? los.ToString(timePattern, culture)
            : los.ToString($"{datePattern} {timePattern}", culture);

        return (aosText, losText);
    }

    public static string FormatLocal(
        DateTime utc,
        ClockDisplayFormat clockFormat = ClockDisplayFormat.TwelveHour,
        CultureInfo? culture = null,
        bool useUtc = false)
    {
        culture ??= CultureInfo.CurrentUICulture;
        var datePattern = culture.DateTimeFormat.ShortDatePattern;
        var timePattern = GetTimePattern(clockFormat, culture: culture);
        return ToDisplayTime(utc, useUtc).ToString($"{datePattern} {timePattern}", culture);
    }

    public static string FormatScheduleLine(
        DateTime aosUtc,
        DateTime losUtc,
        double maxElevationDeg,
        TimeSpan duration,
        ClockDisplayFormat clockFormat = ClockDisplayFormat.TwelveHour,
        CultureInfo? culture = null)
    {
        var (aos, los) = FormatAosLos(aosUtc, losUtc, clockFormat, culture);
        return $"{aos}–{los} {maxElevationDeg:F1}° {duration:mm\\:ss}";
    }

    public static DateOnly GetLocalDate(DateTime utc) => GetDisplayDate(utc, useUtc: false);

    public static DateOnly GetDisplayDate(DateTime utc, bool useUtc) =>
        DateOnly.FromDateTime(ToDisplayTime(utc, useUtc));

    public static string FormatMonthYear(DateTime utc, CultureInfo? culture = null, bool useUtc = false)
    {
        culture ??= CultureInfo.CurrentUICulture;
        return ToDisplayTime(utc, useUtc).ToString("MMMM yyyy", culture);
    }

    public static string FormatDayHeader(DateTime utc, CultureInfo? culture = null, bool useUtc = false)
    {
        culture ??= CultureInfo.CurrentUICulture;
        return ToDisplayTime(utc, useUtc).ToString("D", culture);
    }

    public static (string Aos, string Los) FormatLocalTimes(
        DateTime aosUtc,
        DateTime losUtc,
        CultureInfo? culture = null,
        bool useUtc = false,
        ClockDisplayFormat clockFormat = ClockDisplayFormat.TwelveHour)
    {
        culture ??= CultureInfo.CurrentUICulture;
        var timePattern = GetTimePattern(clockFormat, culture: culture);
        var aos = ToDisplayTime(aosUtc, useUtc).ToString(timePattern, culture);
        var los = ToDisplayTime(losUtc, useUtc).ToString(timePattern, culture);
        return (aos, los);
    }

    public static string FormatScheduleLineTimesOnly(
        DateTime aosUtc,
        DateTime losUtc,
        double maxElevationDeg,
        TimeSpan duration,
        ClockDisplayFormat clockFormat = ClockDisplayFormat.TwelveHour,
        CultureInfo? culture = null)
    {
        var (aos, los) = FormatLocalTimes(aosUtc, losUtc, culture, clockFormat: clockFormat);
        return $"{aos}–{los} {maxElevationDeg:F1}° {duration:mm\\:ss}";
    }

    public static string FormatTimeRangeLine(
        DateTime aosUtc,
        DateTime losUtc,
        CultureInfo? culture = null,
        bool useUtc = false,
        ClockDisplayFormat clockFormat = ClockDisplayFormat.TwelveHour)
    {
        var (aos, los) = FormatLocalTimes(aosUtc, losUtc, culture, useUtc, clockFormat);
        return $"{aos} to {los}";
    }

    /// <summary>Compact range for pass planner grid, e.g. <c>21/05 16:01–16:05</c>.</summary>
    public static string FormatPlannerAosLosLine(
        DateTime aosUtc,
        DateTime losUtc,
        CultureInfo? culture = null,
        bool useUtc = false,
        ClockDisplayFormat clockFormat = ClockDisplayFormat.TwelveHour)
    {
        culture ??= CultureInfo.CurrentUICulture;
        var timePattern = GetTimePattern(clockFormat, culture: culture);
        var aos = ToDisplayTime(aosUtc, useUtc);
        var los = ToDisplayTime(losUtc, useUtc);
        var aosPart = aos.ToString($"dd/MM {timePattern}", culture);
        var losPart = aos.Date == los.Date
            ? los.ToString(timePattern, culture)
            : los.ToString($"dd/MM {timePattern}", culture);
        return $"{aosPart}–{losPart}";
    }

    /// <summary>TCA time for pass planner; includes date when TCA is on a different day than AOS.</summary>
    public static string FormatPlannerTca(
        DateTime tcaUtc,
        DateTime aosUtc,
        CultureInfo? culture = null,
        bool useUtc = false,
        ClockDisplayFormat clockFormat = ClockDisplayFormat.TwelveHour)
    {
        culture ??= CultureInfo.CurrentUICulture;
        var timePattern = GetTimePattern(clockFormat, culture: culture);
        var tca = ToDisplayTime(tcaUtc, useUtc);
        if (tca.Date == ToDisplayTime(aosUtc, useUtc).Date)
            return tca.ToString(timePattern, culture);

        return tca.ToString($"dd/MM {timePattern}", culture);
    }

    public static string FormatDurationMinutes(TimeSpan duration)
    {
        var minutes = duration.TotalSeconds < 30
            ? 0
            : (int)Math.Round(duration.TotalMinutes, MidpointRounding.AwayFromZero);
        return minutes == 1 ? "1 min" : $"{minutes} min";
    }

    public static string FormatDurationLong(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
            return $"{(int)duration.TotalDays} d {duration.Hours} h";

        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours} h {duration.Minutes} min";

        return FormatDurationMinutes(duration);
    }

    public static string FormatDetailsLine(double maxElevationDeg, TimeSpan duration)
    {
        var maxEl = $"{maxElevationDeg:F0}°";
        return $"Duration: {FormatDurationMinutes(duration)} | Max Elevation: {maxEl}";
    }

    public static string FormatOverlapDurationPrecise(TimeSpan duration)
    {
        var totalSeconds = (int)Math.Round(duration.TotalSeconds, MidpointRounding.AwayFromZero);
        if (totalSeconds < 60)
            return totalSeconds == 1 ? "1 second" : $"{totalSeconds} seconds";

        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        if (seconds == 0)
            return minutes == 1 ? "1 minute" : $"{minutes} minutes";

        var minutePart = minutes == 1 ? "1 minute" : $"{minutes} minutes";
        var secondPart = seconds == 1 ? "1 second" : $"{seconds} seconds";
        return $"{minutePart} and {secondPart}";
    }

    public static string FormatMutualOverlapStart(
        DateTime startUtc,
        bool useUtc,
        ClockDisplayFormat clockFormat = ClockDisplayFormat.TwelveHour,
        CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;
        var start = ToDisplayTime(startUtc, useUtc);
        var date = start.ToString(culture.DateTimeFormat.ShortDatePattern, culture);
        var time = start.ToString(GetTimePattern(clockFormat, includeSeconds: true, culture), culture);
        return $"{date} from {time}";
    }

    public static string FormatTimeZoneLabel(bool useUtc)
    {
        if (useUtc)
            return "UTC";

        var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);
        if (offset == TimeSpan.Zero)
            return "UTC";

        var totalHours = offset.TotalHours;
        if (Math.Abs(totalHours - Math.Round(totalHours)) < 0.01)
        {
            var hours = (int)Math.Round(totalHours);
            return hours >= 0 ? $"UTC+{hours}" : $"UTC{hours}";
        }

        var sign = offset >= TimeSpan.Zero ? "+" : "-";
        return $"UTC{sign}{offset:hh\\:mm}";
    }

    public static string FormatMutualWindowLine(
        DateTime startUtc,
        DateTime endUtc,
        CultureInfo? culture = null,
        bool useUtc = false,
        ClockDisplayFormat clockFormat = ClockDisplayFormat.TwelveHour)
    {
        culture ??= CultureInfo.CurrentUICulture;
        var timePattern = GetTimePattern(clockFormat, culture: culture);
        var start = ToDisplayTime(startUtc, useUtc);
        var end = ToDisplayTime(endUtc, useUtc);
        var startPart = start.ToString(timePattern, culture);
        var endPart = start.Date == end.Date
            ? end.ToString(timePattern, culture)
            : end.ToString($"dd/MM {timePattern}", culture);
        return $"{startPart}–{endPart}";
    }

    public static string FormatStatusClock(
        DateTime utc,
        ClockDisplayFormat clockFormat,
        bool useUtc,
        CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;
        var timePattern = GetTimePattern(clockFormat, includeSeconds: true, culture: culture);
        return ToDisplayTime(utc, useUtc).ToString($"yyyy-MM-dd {timePattern}", culture);
    }

    public static string FormatUtcClock(DateTime utc, ClockDisplayFormat clockFormat, CultureInfo? culture = null) =>
        FormatStatusClock(utc, clockFormat, useUtc: true, culture);

    public static string FormatHoverTime(
        DateTime utc,
        bool useUtc,
        ClockDisplayFormat clockFormat = ClockDisplayFormat.TwelveHour,
        CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;
        return ToDisplayTime(utc, useUtc).ToString(GetTimePattern(clockFormat, includeSeconds: true, culture), culture);
    }

    /// <summary>Compact time label for chart axes (no date or seconds).</summary>
    public static string FormatAxisTime(
        DateTime utc,
        bool useUtc,
        ClockDisplayFormat clockFormat = ClockDisplayFormat.TwelveHour,
        CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;
        return ToDisplayTime(utc, useUtc).ToString(GetTimePattern(clockFormat, culture: culture), culture);
    }

    /// <summary>Minutes and seconds until AOS, e.g. <c>14:05</c>.</summary>
    public static string FormatCountdownToAos(DateTime utcNow, DateTime aosUtc)
    {
        var delta = aosUtc - utcNow;
        if (delta <= TimeSpan.Zero)
            return "0:00";

        return $"{(int)delta.TotalMinutes}:{delta.Seconds:D2}";
    }

    /// <summary>Total hours, minutes, and seconds until an event, e.g. <c>36:15:05</c>.</summary>
    public static string FormatCountdownHms(TimeSpan delta)
    {
        if (delta <= TimeSpan.Zero)
            return "0:00:00";

        return $"{(int)delta.TotalHours}:{delta.Minutes:D2}:{delta.Seconds:D2}";
    }

    public static ClockDisplayFormat FromSettings(bool use24HourClock) =>
        use24HourClock ? ClockDisplayFormat.TwentyFourHour : ClockDisplayFormat.TwelveHour;

    /// <summary>Compact pass label for radar gallery cards, e.g. <c>Today 18:56</c>.</summary>
    public static string FormatGalleryPassTime(
        DateTime aosUtc,
        bool useUtc,
        ClockDisplayFormat clockFormat,
        CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;
        var aos = ToDisplayTime(aosUtc, useUtc);
        var dayLabel = HamsAtDisplayFormat.FormatRelativeDay(aos, culture);
        var timePattern = GetTimePattern(clockFormat, culture: culture);
        return $"{dayLabel} {aos.ToString(timePattern, culture)}";
    }

    private static DateTime ToDisplayTime(DateTime utc, bool useUtc) =>
        useUtc ? EnsureUtc(utc) : ToLocal(utc);

    private static DateTime ToLocal(DateTime utc)
    {
        return utc.Kind switch
        {
            DateTimeKind.Utc => utc.ToLocalTime(),
            DateTimeKind.Local => utc,
            _ => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime()
        };
    }

    private static DateTime EnsureUtc(DateTime utc)
    {
        return utc.Kind switch
        {
            DateTimeKind.Utc => utc,
            DateTimeKind.Local => utc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(utc, DateTimeKind.Utc)
        };
    }
}
