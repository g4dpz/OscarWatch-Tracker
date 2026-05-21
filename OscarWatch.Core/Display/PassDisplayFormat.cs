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

    public static string FormatDayHeader(DateTime utc, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        return ToLocal(utc).ToString("D", culture);
    }

    public static (string Aos, string Los) FormatLocalTimes(
        DateTime aosUtc,
        DateTime losUtc,
        CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var timePattern = culture.DateTimeFormat.ShortTimePattern;
        var aos = ToLocal(aosUtc).ToString(timePattern, culture);
        var los = ToLocal(losUtc).ToString(timePattern, culture);
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

    /// <summary>Compact local range for pass planner grid, e.g. <c>21/05 16:01–16:05</c>.</summary>
    public static string FormatPlannerAosLosLine(
        DateTime aosUtc,
        DateTime losUtc,
        CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var timePattern = culture.DateTimeFormat.ShortTimePattern;
        var aosLocal = ToLocal(aosUtc);
        var losLocal = ToLocal(losUtc);
        var aosPart = aosLocal.ToString($"dd/MM {timePattern}", culture);
        var losPart = aosLocal.Date == losLocal.Date
            ? losLocal.ToString(timePattern, culture)
            : losLocal.ToString($"dd/MM {timePattern}", culture);
        return $"{aosPart}–{losPart}";
    }

    /// <summary>TCA time for pass planner; includes date when TCA is on a different local day than AOS.</summary>
    public static string FormatPlannerTca(DateTime tcaUtc, DateTime aosUtc, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var timePattern = culture.DateTimeFormat.ShortTimePattern;
        var tcaLocal = ToLocal(tcaUtc);
        if (tcaLocal.Date == ToLocal(aosUtc).Date)
            return tcaLocal.ToString(timePattern, culture);

        return tcaLocal.ToString($"dd/MM {timePattern}", culture);
    }

    public static string FormatDurationMinutes(TimeSpan duration)
    {
        var minutes = duration.TotalSeconds < 30
            ? 0
            : (int)Math.Round(duration.TotalMinutes, MidpointRounding.AwayFromZero);
        return minutes == 1 ? "1 min" : $"{minutes} min";
    }

    public static string FormatDetailsLine(double maxElevationDeg, TimeSpan duration)
    {
        var maxEl = $"{maxElevationDeg:F0}°";
        return $"Duration: {FormatDurationMinutes(duration)} | Max Elevation: {maxEl}";
    }

    /// <summary>Minutes and seconds until AOS, e.g. <c>14:05</c>.</summary>
    public static string FormatCountdownToAos(DateTime utcNow, DateTime aosUtc)
    {
        var delta = aosUtc - utcNow;
        if (delta <= TimeSpan.Zero)
            return "0:00";

        return $"{(int)delta.TotalMinutes}:{delta.Seconds:D2}";
    }

    private static DateTime ToLocal(DateTime utc)
    {
        return utc.Kind switch
        {
            DateTimeKind.Utc => utc.ToLocalTime(),
            DateTimeKind.Local => utc,
            _ => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime()
        };
    }
}
