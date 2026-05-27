using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

public class SatelliteDatabaseFileSerializeTests
{
    [Fact]
    public void SerializeEntries_round_trips_through_parse()
    {
        var entries = new List<SatelliteRadioEntry>
        {
            new()
            {
                Name = "TEST-1",
                Modes =
                [
                    new SatelliteTransponderMode
                    {
                        Type = "FM",
                        DownlinkKHz = 436_795,
                        UplinkKHz = 145_850,
                        DownlinkMode = "FMN",
                        UplinkMode = "FMN",
                        Doppler = "NOR"
                    }
                ]
            }
        };

        var json = SatelliteDatabaseFile.SerializeEntries(entries);
        var parsed = SatelliteDatabaseFile.ParseJson(json);

        Assert.Single(parsed);
        Assert.Equal("TEST-1", parsed[0].Name);
        Assert.Equal("FM", parsed[0].Modes[0].Type);
    }
}
