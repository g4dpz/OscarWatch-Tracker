using OscarWatch.Core.Models;

namespace OscarWatch.Core.Export;

public static class GoogleCalendarEventUrl
{
    public const string RenderEndpoint = "https://calendar.google.com/calendar/render";

    public static string Build(PassInfo pass, GroundStation station)
    {
        ArgumentNullException.ThrowIfNull(pass);
        ArgumentNullException.ThrowIfNull(station);

        var dates = $"{IcsPassExporter.FormatUtc(pass.AosUtc)}/{IcsPassExporter.FormatUtc(pass.LosUtc)}";
        var query = string.Join(
            "&",
            "action=TEMPLATE",
            "text=" + Uri.EscapeDataString(IcsPassExporter.BuildEventSummary(pass)),
            "dates=" + dates,
            "details=" + Uri.EscapeDataString(IcsPassExporter.BuildEventDescription(pass)),
            "location=" + Uri.EscapeDataString(IcsPassExporter.BuildEventLocation(station)));

        return $"{RenderEndpoint}?{query}";
    }
}
