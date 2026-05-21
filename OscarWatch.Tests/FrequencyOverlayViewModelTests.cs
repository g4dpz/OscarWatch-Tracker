using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
using OscarWatch.ViewModels;

namespace OscarWatch.Tests;

public class FrequencyOverlayViewModelTests
{
    [Fact]
    public void Mode_switch_persists_offsets_for_new_mode_not_previous_spinner_values()
    {
        var settings = new TestSettingsService();
        settings.Current.FrequencySelections["RS-44"] = new SatelliteFrequencySelection
        {
            ModeType = "SSB Transponder",
            ModeIndex = 0,
            RememberOffsets = true
        };
        settings.Current.FrequencySelections["RS-44"].SetOffsetsForMode("SSB Transponder", 0, 4.025);
        settings.Current.FrequencySelections["RS-44"].SetOffsetsForMode("FT4", 0, 0);

        var database = new TestSatelliteDatabaseService(
        [
            new SatelliteRadioEntry
            {
                Name = "RS-44",
                Modes =
                [
                    new SatelliteTransponderMode
                    {
                        Type = "SSB Transponder",
                        DownlinkKHz = 435_667,
                        UplinkKHz = 145_937.61,
                        DownlinkMode = "USB",
                        UplinkMode = "LSB",
                        Doppler = "REV"
                    },
                    new SatelliteTransponderMode
                    {
                        Type = "FT4",
                        DownlinkKHz = 435_611,
                        UplinkKHz = 145_993.61,
                        DownlinkMode = "DATA-USB",
                        UplinkMode = "DATA-LSB",
                        Doppler = "REV"
                    }
                ]
            }
        ]);

        var vm = new FrequencyOverlayViewModel(settings, database);
        vm.Update(new SatelliteTrackState
        {
            Name = "RS-44",
            NoradId = "99999",
            Subpoint = new GeoCoordinate(57, 18),
            LookAngles = new LookAngles(180, 25, 800, 2.5)
        });

        Assert.Equal("SSB Transponder", vm.SelectedMode?.Type);
        Assert.InRange(vm.ReceiveOffsetKHz, 4.024, 4.026);

        vm.SelectedMode = vm.AvailableModes.First(m => m.Type == "FT4");

        Assert.InRange(vm.ReceiveOffsetKHz, -0.001, 0.001);
        var stored = settings.Current.FrequencySelections["RS-44"];
        Assert.InRange(stored.ModeOffsets["FT4"].ReceiveOffsetKHz, -0.001, 0.001);
        Assert.InRange(stored.ModeOffsets["SSB Transponder"].ReceiveOffsetKHz, 4.024, 4.026);
    }

    private sealed class TestSettingsService : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public string SettingsPath { get; } = Path.Combine(Path.GetTempPath(), "oscarwatch-test-settings.json");
        public void Load() { }
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void SyncGridFromLatLon() { }
        public void SyncLatLonFromGrid() { }
        public void EnsureSavedStations() { }
        public void ApplyActiveStation() { }
        public void SyncActiveStationFromGroundStation() { }
    }

    private sealed class TestSatelliteDatabaseService(IReadOnlyList<SatelliteRadioEntry> entries) : ISatelliteDatabaseService
    {
        public IReadOnlyList<SatelliteRadioEntry> Entries { get; } = entries;
        public string ActiveDatabasePath { get; } = "test";
        public bool IsUsingUserDatabase => false;

        public SatelliteRadioEntry? TryGetEntry(string satelliteName) =>
            Entries.FirstOrDefault(e => e.Name.Equals(satelliteName, StringComparison.OrdinalIgnoreCase));

        public void Reload() { }
    }
}
