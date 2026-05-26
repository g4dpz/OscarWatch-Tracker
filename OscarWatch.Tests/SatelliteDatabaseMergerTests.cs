using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

public class SatelliteDatabaseMergerTests
{
    [Fact]
    public void BuildPlan_adds_new_satellite_and_mode()
    {
        var local = new List<SatelliteRadioEntry>
        {
            new()
            {
                Name = "SO-50",
                Modes =
                [
                    new SatelliteTransponderMode
                    {
                        Type = "FM VOICE",
                        DownlinkKHz = 436_795,
                        UplinkKHz = 145_850,
                        DownlinkMode = "FMN",
                        UplinkMode = "FMN",
                        Doppler = "NOR"
                    }
                ]
            }
        };

        var remote = new List<SatelliteRadioEntry>
        {
            new()
            {
                Name = "SO-50",
                Modes =
                [
                    new SatelliteTransponderMode
                    {
                        Type = "FM VOICE",
                        DownlinkKHz = 436_795,
                        UplinkKHz = 145_850,
                        DownlinkMode = "FMN",
                        UplinkMode = "FMN",
                        Doppler = "NOR"
                    },
                    new SatelliteTransponderMode
                    {
                        Type = "Telemetry",
                        DownlinkKHz = 435_000,
                        UplinkKHz = 0,
                        DownlinkMode = "FM",
                        UplinkMode = "FM",
                        Doppler = "NOR"
                    }
                ]
            },
            new()
            {
                Name = "BOTAN",
                Modes =
                [
                    new SatelliteTransponderMode
                    {
                        Type = "Packet",
                        DownlinkKHz = 145_825,
                        UplinkKHz = 145_825,
                        DownlinkMode = "FM",
                        UplinkMode = "FM",
                        Doppler = "NOR"
                    }
                ]
            }
        };

        var plan = SatelliteDatabaseMerger.BuildPlan(local, remote);

        Assert.Single(plan.NewSatellites);
        Assert.Equal("BOTAN", plan.NewSatellites[0].Entry.Name);
        Assert.Single(plan.NewModes);
        Assert.Equal("SO-50", plan.NewModes[0].SatelliteName);
        Assert.Equal("Telemetry", plan.NewModes[0].Mode.Type);
        Assert.Empty(plan.Conflicts);
    }

    [Fact]
    public void BuildPlan_detects_conflicting_mode_fields()
    {
        var local = new List<SatelliteRadioEntry>
        {
            new()
            {
                Name = "ISS",
                Modes =
                [
                    new SatelliteTransponderMode
                    {
                        Type = "Packet",
                        DownlinkKHz = 145_825,
                        UplinkKHz = 145_825,
                        DownlinkMode = "FM",
                        UplinkMode = "FM",
                        Doppler = "NOR"
                    }
                ]
            }
        };

        var remote = new List<SatelliteRadioEntry>
        {
            new()
            {
                Name = "ISS",
                Modes =
                [
                    new SatelliteTransponderMode
                    {
                        Type = "Packet",
                        DownlinkKHz = 145_825,
                        UplinkKHz = 145_825,
                        DownlinkMode = "FMN",
                        UplinkMode = "FMN",
                        Doppler = "NOR"
                    }
                ]
            }
        };

        var plan = SatelliteDatabaseMerger.BuildPlan(local, remote);

        Assert.Empty(plan.NewSatellites);
        Assert.Empty(plan.NewModes);
        Assert.Single(plan.Conflicts);
        Assert.Equal("ISS", plan.Conflicts[0].SatelliteName);
        Assert.Equal("Packet", plan.Conflicts[0].ModeType);
    }

    [Fact]
    public void Apply_adds_selected_entries_and_resolves_conflicts()
    {
        var local = new List<SatelliteRadioEntry>
        {
            new()
            {
                Name = "ISS",
                Modes =
                [
                    new SatelliteTransponderMode
                    {
                        Type = "Packet",
                        DownlinkKHz = 145_825,
                        UplinkKHz = 145_825,
                        DownlinkMode = "FM",
                        UplinkMode = "FM",
                        Doppler = "NOR"
                    }
                ]
            }
        };

        var remote = new List<SatelliteRadioEntry>
        {
            new()
            {
                Name = "ISS",
                Modes =
                [
                    new SatelliteTransponderMode
                    {
                        Type = "Packet",
                        DownlinkKHz = 145_825,
                        UplinkKHz = 145_825,
                        DownlinkMode = "FMN",
                        UplinkMode = "FMN",
                        Doppler = "NOR"
                    }
                ]
            },
            new()
            {
                Name = "BOTAN",
                Modes =
                [
                    new SatelliteTransponderMode
                    {
                        Type = "Packet",
                        DownlinkKHz = 145_825,
                        UplinkKHz = 145_825,
                        DownlinkMode = "FM",
                        UplinkMode = "FM",
                        Doppler = "NOR"
                    }
                ]
            }
        };

        var plan = SatelliteDatabaseMerger.BuildPlan(local, remote);
        var selection = new SatelliteDatabaseMergeSelection
        {
            AcceptedNewSatelliteKeys = { "BOTAN" },
            AcceptRemoteConflictKeys = { plan.Conflicts[0].Key }
        };

        var merged = SatelliteDatabaseMerger.Apply(local, plan, selection);

        Assert.Equal(2, merged.Count);
        var iss = merged.First(e => e.Name == "ISS");
        Assert.Equal("FMN", iss.Modes[0].DownlinkMode);
        Assert.Contains(merged, e => e.Name == "BOTAN");
    }

    [Fact]
    public void ParseJson_accepts_string_frequencies()
    {
        const string json = """
            [
              {
                "name": "TEST",
                "modes": [
                  {
                    "type": "SSTV",
                    "downlink": "437350",
                    "uplink": 0,
                    "downlink_mode": "FM",
                    "uplink_mode": "FM",
                    "doppler": "NOR"
                  }
                ]
              }
            ]
            """;

        var entries = SatelliteDatabaseFile.ParseJson(json);

        Assert.Single(entries);
        Assert.Equal(437_350, entries[0].Modes[0].DownlinkKHz);
    }
}
