using System.Globalization;

namespace OscarWatch.Core.Display;

public static class PassDisplayFormat
{
    public static (string Aos, string Los) FormatAosLos(
        DateTime aosUtc,
        DateTime losUtc,
        CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var datePattern = culture.DateTimeFormat.ShortDatePattern;
        var timePattern = culture.DateTimeFormat.ShortTimePattern;

        var aos = ToLocal(aosUtc);
        var los = ToLocal(losUtc);
        var aosText = aos.ToString($"{datePattern} {timePattern}", culture);
        var losText = aos.Date == los.Date
            ? los.ToString(timePattern, culture)
            : los.ToString($"{datePattern} {timePattern}", culture);

        return (aosText, losText);
    }

    public static string FormatLocal(DateTime utc, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var datePattern = culture.DateTimeFormat.ShortDatePattern;
        var timePattern = culture.DateTimeFormat.ShortTimePattern;
        return ToLocal(utc).ToString($"{datePattern} {timePattern}", culture);
    }

    public static string FormatScheduleLine(
        DateTime aosUtc,
        DateTime losUtc,
        double maxElevationDeg,
        TimeSpan duration,
        CultureInfo? culture = null)
    {
        var (aos, los) = FormatAosLos(aosUtc, losUtc, culture);
        return $"{aos}–{los} {maxElevationDeg:F1}° {duration:mm\\:ss}";
    }

    public static DateOnly GetLocalDate(DateTime utc) => DateOnly.FromDateTime(ToLocal(utc));

    public static string FormatMonthYear(DateTime utc, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        return ToLocal(utc).ToString("MMMM yyyy", culture);
    }

    public static string FormatDayHeader(DateTime utc, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        return ToLocal(utc).ToString("D", culture);
    }

    public static (string Aos, string Los) FormatLocalTimes(
        DateTime aosUtc,
        DateTime losUtc,
        CultureInfo? culture = null,
        bool useUtc = false)
    {
        culture ??= CultureInfo.CurrentCulture;
        var timePattern = useUtc ? "HH:mm" : culture.DateTimeFormat.ShortTimePattern;
        var aos = ToDisplayTime(aosUtc, useUtc).ToString(timePattern, culture);
        var los = ToDisplayTime(losUtc, useUtc).ToString(timePattern, culture);
        return (aos, los);
    }

    public static string FormatScheduleLineTimesOnly(
        DateTime aosUtc,
        DateTime losUtc,
        double maxElevationDeg,
        TimeSpan duration,
        CultureInfo? culture = null)
    {
        var (aos, los) = FormatLocalTimes(aosUtc, losUtc, culture);
        return $"{aos}–{los} {maxElevationDeg:F1}° {duration:mm\\:ss}";
    }

    public static string FormatTimeRangeLine(
        DateTime aosUtc,
        DateTime losUtc,
        CultureInfo? culture = null)
    {
        var (aos, los) = FormatLocalTimes(aosUtc, losUtc, culture);
        return $"{aos} to {los}";
    }

    /// <summary>Compact range for pass planner grid, e.g. <c>21/05 16:01–16:05</c>.</summary>
    public static string FormatPlannerAosLosLine(
        DateTime aosUtc,
        DateTime losUtc,
        CultureInfo? culture = null,
        bool useUtc = false)
    {
        culture ??= CultureInfo.CurrentCulture;
        var timePattern = useUtc ? "HH:mm" : culture.DateTimeFormat.ShortTimePattern;
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
        bool useUtc = false)
    {
        culture ??= CultureInfo.CurrentCulture;
        var timePattern = useUtc ? "HH:mm" : culture.DateTimeFormat.ShortTimePattern;
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

    public static string FormatMutualWindowLine(
        DateTime startUtc,
        DateTime endUtc,
        CultureInfo? culture = null,
        bool useUtc = false)
    {
        culture ??= CultureInfo.CurrentCulture;
        var timePattern = useUtc ? "HH:mm" : culture.DateTimeFormat.ShortTimePattern;
        var start = ToDisplayTime(startUtc, useUtc);
        var end = ToDisplayTime(endUtc, useUtc);
        var startPart = start.ToString(timePattern, culture);
        var endPart = start.Date == end.Date
            ? end.ToString(timePattern, culture)
            : end.ToString($"dd/MM {timePattern}", culture);
        return $"{startPart}–{endPart}";
    }

    /// <summary>Minutes and seconds until AOS, e.g. <c>14:05</c>.</summary>
    public static string FormatCountdownToAos(DateTime utcNow, DateTime aosUtc)
    {
        var delta = aosUtc - utcNow;
        if (delta <= TimeSpan.Zero)
            return "0:00";

        return $"{(int)delta.TotalMinutes}:{delta.Seconds:D2}";
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
