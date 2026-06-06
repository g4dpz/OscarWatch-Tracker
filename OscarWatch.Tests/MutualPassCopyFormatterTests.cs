using System.Globalization;
using OscarWatch.Core.Display;
using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

public class MutualPassCopyFormatterTests
{
    private static readonly MutualPassCopyFormatter.Labels TestLabels = new()
    {
        Title = "Mutual pass — {0}",
        Between = "Between {0} and {1}",
        TimesIn = "Times shown in {0}",
        MutualWindowHeader = "Mutual window",
        MutualWindowLine = "  {0} ({1})",
        YourPassHeader = "Your pass — {0}",
        RemotePassHeader = "Remote pass — {0}",
        PassTimes = "  {0}",
        MaxElevation = "  Max elevation {0:F1}°",
        Azimuth = "  AOS azimuth {0:F1}° · LOS azimuth {1:F1}°"
    };

    [Fact]
    public void Format_includes_satellite_stations_and_pass_details()
    {
        var aos = new DateTime(2026, 6, 6, 14, 28, 0, DateTimeKind.Utc);
        var los = new DateTime(2026, 6, 6, 14, 42, 0, DateTimeKind.Utc);
        var mutualStart = new DateTime(2026, 6, 6, 14, 32, 0, DateTimeKind.Utc);
        var mutualEnd = new DateTime(2026, 6, 6, 14, 38, 0, DateTimeKind.Utc);

        var pass = new MutualPassInfo
        {
            SatelliteName = "ISS (ZARYA)",
            NoradId = "25544",
            MutualStartUtc = mutualStart,
            MutualEndUtc = mutualEnd,
            LocalPass = new PassInfo
            {
                SatelliteName = "ISS (ZARYA)",
                NoradId = "25544",
                AosUtc = aos,
                LosUtc = los,
                MaxElevationUtc = mutualStart,
                MaxElevationDeg = 42.3,
                AosAzimuthDeg = 127.4,
                LosAzimuthDeg = 248.1
            },
            RemotePass = new PassInfo
            {
                SatelliteName = "ISS (ZARYA)",
                NoradId = "25544",
                AosUtc = aos.AddMinutes(2),
                LosUtc = los.AddMinutes(-2),
                MaxElevationUtc = mutualStart,
                MaxElevationDeg = 38.1,
                AosAzimuthDeg = 135.2,
                LosAzimuthDeg = 255.0
            }
        };

        var local = new GroundStation
        {
            DisplayName = "London",
            LatitudeDeg = 51.5,
            LongitudeDeg = -0.1,
            GridSquare = "IO91"
        };
        var remote = new GroundStation
        {
            DisplayName = "Dave",
            LatitudeDeg = 52.0,
            LongitudeDeg = 5.0,
            GridSquare = "JO22"
        };

        var text = MutualPassCopyFormatter.Format(
            pass,
            local,
            remote,
            TestLabels,
            useUtc: true,
            ClockDisplayFormat.TwentyFourHour,
            CultureInfo.InvariantCulture);

        Assert.Contains("Mutual pass — ISS (ZARYA)", text);
        Assert.Contains("Between London (IO91) and Dave (JO22)", text);
        Assert.Contains("Times shown in UTC", text);
        Assert.Contains("Mutual window", text);
        Assert.Contains("Your pass — London (IO91)", text);
        Assert.Contains("Remote pass — Dave (JO22)", text);
        Assert.Contains("42.3°", text);
        Assert.Contains("38.1°", text);
        Assert.Contains("127.4°", text);
        Assert.Contains("255.0°", text);
    }
}
