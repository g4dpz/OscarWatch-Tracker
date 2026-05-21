using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

public class SatelliteDatabaseFileTests
{
    [Fact]
    public void Save_and_load_round_trip_preserves_mode_fields()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ow-db-{Guid.NewGuid():N}.json");
        try
        {
            var entries = new List<SatelliteRadioEntry>
            {
                new()
                {
                    Name = "TEST-SAT",
                    Modes =
                    [
                        new SatelliteTransponderMode
                        {
                            Type = "FM VOICE",
                            DownlinkKHz = 436_795,
                            UplinkKHz = 145_850,
                            DownlinkMode = "FMN",
                            UplinkMode = "FMN",
                            Doppler = "NOR",
                            CtcssHz = 67.0,
                            CtcssArmHz = 74.4
                        }
                    ]
                }
            };

            SatelliteDatabaseFile.Save(path, entries);
            var loaded = SatelliteDatabaseFile.Load(path);

            Assert.Single(loaded);
            Assert.Equal("TEST-SAT", loaded[0].Name);
            var mode = loaded[0].Modes[0];
            Assert.Equal(436_795, mode.DownlinkKHz);
            Assert.Equal(67.0, mode.CtcssHz);
            Assert.Equal(74.4, mode.CtcssArmHz);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void ValidateEntries_rejects_duplicate_names()
    {
        var entries = new List<SatelliteRadioEntry>
        {
            new() { Name = "ISS", Modes = [new SatelliteTransponderMode { Type = "A", DownlinkKHz = 1, UplinkKHz = 1, DownlinkMode = "FM", UplinkMode = "FM" }] },
            new() { Name = "iss", Modes = [new SatelliteTransponderMode { Type = "B", DownlinkKHz = 1, UplinkKHz = 1, DownlinkMode = "FM", UplinkMode = "FM" }] }
        };

        Assert.NotNull(SatelliteDatabaseFile.ValidateEntries(entries));
    }
}
