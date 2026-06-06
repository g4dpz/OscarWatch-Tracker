using System.Globalization;
using OscarWatch.Core.Models;

namespace OscarWatch.Core.Display;

public static class HamsAtDisplayFormat
{
    public static string FormatGrids(IReadOnlyList<string> grids) =>
        grids.Count == 0 ? "" : string.Join(", ", grids);

    public static string FormatAlertWindow(
        DateTime aosUtc,
        DateTime losUtc,
        bool useUtc,
        ClockDisplayFormat clockFormat,
        CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;
        var aos = ToDisplayTime(aosUtc, useUtc);
        var los = ToDisplayTime(losUtc, useUtc);
        var timePattern = PassDisplayFormat.GetTimePattern(clockFormat, culture: culture);
        var aosDay = FormatRelativeDay(aos, culture);
        var losDay = FormatRelativeDay(los, culture);
        var aosTime = aos.ToString(timePattern, culture);
        var losTime = los.ToString(timePattern, culture);

        if (aosDay == losDay)
            return $"{aosDay} {aosTime} – {losTime}";

        return $"{aosDay} {aosTime} – {losDay} {losTime}";
    }

    public static string FormatRelativeDay(DateTime displayTime, CultureInfo culture)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var day = DateOnly.FromDateTime(displayTime);
        if (day == today)
            return GetTodayLabel(culture);
        if (day == today.AddDays(1))
            return GetTomorrowLabel(culture);
        if (day == today.AddDays(-1))
            return GetYesterdayLabel(culture);

        return displayTime.ToString(culture.DateTimeFormat.ShortDatePattern, culture);
    }

    private static string GetTodayLabel(CultureInfo culture) =>
        culture.TwoLetterISOLanguageName switch
        {
            "ja" => "今日",
            "zh" => "今天",
            "pt" => "Hoje",
            _ => "Today"
        };

    private static string GetTomorrowLabel(CultureInfo culture) =>
        culture.TwoLetterISOLanguageName switch
        {
            "ja" => "明日",
            "zh" => "明天",
            "pt" => "Amanhã",
            _ => "Tomorrow"
        };

    private static string GetYesterdayLabel(CultureInfo culture) =>
        culture.TwoLetterISOLanguageName switch
        {
            "ja" => "昨日",
            "zh" => "昨天",
            "pt" => "Ontem",
            _ => "Yesterday"
        };

    private static DateTime ToDisplayTime(DateTime utc, bool useUtc) =>
        useUtc
            ? utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc)
            : utc.Kind == DateTimeKind.Utc ? utc.ToLocalTime() : utc;
}
