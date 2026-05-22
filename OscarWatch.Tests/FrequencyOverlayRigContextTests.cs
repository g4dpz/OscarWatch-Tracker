using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
using OscarWatch.ViewModels;

namespace OscarWatch.Tests;

public class FrequencyOverlayRigContextTests
{
    [Fact]
    public void TryBuildRigTrackingContext_returns_null_when_overlay_not_synced_to_state()
    {
        var settings = new TestSettingsService();
        var database = new TestSatelliteDatabaseService(
        [
            new SatelliteRadioEntry
            {
                Name = "FO-29",
                Modes =
                [
                    new SatelliteTransponderMode
                    {
                        Type = "SSB Transponder",
                        DownlinkKHz = 435_850.45,
                        UplinkKHz = 145_952.65,
                        DownlinkMode = "USB",
                        UplinkMode = "LSB",
                        Doppler = "REV"
                    }
                ]
            },
            new SatelliteRadioEntry
            {
                Name = "JO-97",
                Modes =
                [
                    new SatelliteTransponderMode
                    {
                        Type = "SSB Transponder",
                        DownlinkKHz = 145_865,
                        UplinkKHz = 435_110.1,
                        DownlinkMode = "USB",
                        UplinkMode = "LSB",
                        Doppler = "REV"
                    }
                ]
            }
        ]);

        var vm = new FrequencyOverlayViewModel(settings, database);
        vm.Update(new SatelliteTrackState
        {
            Name = "FO-29",
            NoradId = "11111",
            Subpoint = new GeoCoordinate(57, 18),
            LookAngles = new LookAngles(180, 25, 800, 2.5)
        });

        var jo97 = new SatelliteTrackState
        {
            Name = "JO-97",
            NoradId = "22222",
            Subpoint = new GeoCoordinate(57, 18),
            LookAngles = new LookAngles(180, 25, 800, 2.5)
        };

        Assert.Null(vm.TryBuildRigTrackingContext(jo97));

        vm.Update(jo97);
        Assert.NotNull(vm.TryBuildRigTrackingContext(jo97));
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
