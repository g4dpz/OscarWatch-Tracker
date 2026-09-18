using OscarWatch.Core.Export;
using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

public sealed class GoogleCalendarEventUrlTests
{
    [Fact]
    public void Build_uses_google_template_with_utc_aos_and_los()
    {
        var aos = new DateTime(2026, 9, 9, 16, 1, 0, DateTimeKind.Utc);
        var los = aos.AddMinutes(8);
        var pass = CreatePass("25544", "ISS", aos, los);
        var station = CreateStation();

        var url = GoogleCalendarEventUrl.Build(pass, station);

        Assert.StartsWith(GoogleCalendarEventUrl.RenderEndpoint + "?", url, StringComparison.Ordinal);
        Assert.Contains("action=TEMPLATE", url, StringComparison.Ordinal);
        Assert.Contains("dates=20260909T160100Z/20260909T160900Z", url, StringComparison.Ordinal);
        Assert.Contains("text=" + Uri.EscapeDataString("ISS pass (max 45.0°)"), url, StringComparison.Ordinal);
        Assert.Contains("location=" + Uri.EscapeDataString("Home (IO91wm)"), url, StringComparison.Ordinal);
        Assert.Contains("details=", url, StringComparison.Ordinal);
        Assert.DoesNotContain(" ", url.Split('?')[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Build_percent_encodes_special_characters_in_query_values()
    {
        var aos = new DateTime(2026, 3, 20, 8, 0, 0, DateTimeKind.Utc);
        var pass = CreatePass("25544", "SAT;with,special chars", aos, aos.AddMinutes(10));
        var station = new GroundStation
        {
            DisplayName = "Home & Garden",
            LatitudeDeg = 51.5,
            LongitudeDeg = -0.1,
            AltitudeMetersAsl = 50,
            GridSquare = "IO91wm"
        };

        var url = GoogleCalendarEventUrl.Build(pass, station);
        var query = url[(url.IndexOf('?', StringComparison.Ordinal) + 1)..];

        Assert.Contains("SAT%3Bwith%2Cspecial%20chars", query, StringComparison.Ordinal);
        Assert.Contains("Home%20%26%20Garden", query, StringComparison.Ordinal);
        Assert.DoesNotContain("SAT;with", query, StringComparison.Ordinal);
        Assert.DoesNotContain("Home &", query, StringComparison.Ordinal);
    }

    private static PassInfo CreatePass(string noradId, string satelliteName, DateTime aos, DateTime los) =>
        new()
        {
            SatelliteName = satelliteName,
            NoradId = noradId,
            AosUtc = aos,
            LosUtc = los,
            MaxElevationDeg = 45.0,
            MaxElevationUtc = aos + (los - aos) / 2,
            AosAzimuthDeg = 180.0,
            LosAzimuthDeg = 0.0
        };

    private static GroundStation CreateStation() =>
        new()
        {
            DisplayName = "Home",
            LatitudeDeg = 51.5,
            LongitudeDeg = -0.1,
            AltitudeMetersAsl = 50,
            GridSquare = "IO91wm"
        };
}
