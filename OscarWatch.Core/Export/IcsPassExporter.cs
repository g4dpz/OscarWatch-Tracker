using System.Text;
using OscarWatch.Core.Models;

namespace OscarWatch.Core.Export;

public static class IcsPassExporter
{
    public static string BuildCalendar(
        IEnumerable<PassInfo> passes,
        GroundStation station,
        string calendarName = "OscarWatch Passes")
    {
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//OscarWatch//Pass Planner//EN");
        sb.AppendLine("CALSCALE:GREGORIAN");
        sb.AppendLine($"X-WR-CALNAME:{Escape(calendarName)}");

        foreach (var pass in passes)
        {
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"UID:{pass.NoradId}-{pass.AosUtc.Ticks}@oscarwatch.org");
            sb.AppendLine($"DTSTAMP:{FormatUtc(DateTime.UtcNow)}");
            sb.AppendLine($"DTSTART:{FormatUtc(pass.AosUtc)}");
            sb.AppendLine($"DTEND:{FormatUtc(pass.LosUtc)}");
            sb.AppendLine($"SUMMARY:{Escape($"{pass.SatelliteName} pass (max {pass.MaxElevationDeg:F1}°)")}");
            sb.AppendLine(
                "DESCRIPTION:" + Escape(
                    $"TCA {pass.MaxElevationUtc:u} UTC ({pass.MaxElevationUtc.ToLocalTime():g} local)\n" +
                    $"Max elevation {pass.MaxElevationDeg:F1}°\n" +
                    $"AOS az {pass.AosAzimuthDeg:F0}° LOS az {pass.LosAzimuthDeg:F0}°\n" +
                    $"Duration {pass.Duration:mm\\:ss}"));
            sb.AppendLine($"LOCATION:{Escape($"{station.DisplayName} ({station.GridSquare})")}");
            sb.AppendLine("END:VEVENT");
        }

        sb.AppendLine("END:VCALENDAR");
        return sb.ToString();
    }

    private static string FormatUtc(DateTime utc) =>
        utc.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'");

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
}
