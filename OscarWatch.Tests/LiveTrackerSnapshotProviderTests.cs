using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
using OscarWatch.Localization;
using OscarWatch.Services;
using OscarWatch.ViewModels;

namespace OscarWatch.Tests;

public class LiveTrackerSnapshotProviderTests
{
    [Fact]
    public void GetCurrent_uses_focused_track_name_when_overlay_still_on_previous_satellite()
    {
        var settings = new TestSettingsService();
        var database = new TestSatelliteDatabaseService(
        [
            CreateJo97Entry(),
            CreateRs44Entry()
        ]);

        var frequencies = new FrequencyOverlayViewModel(settings, database, LocalizationService.Instance);
        frequencies.Update(CreateTrack("JO-97", "43855"));

        var liveTracking = new StubLiveTrackingService(
        [
            CreateTrack("JO-97", "43855"),
            CreateTrack("RS-44", "44909")
        ]);

        var provider = new LiveTrackerSnapshotProvider(frequencies, liveTracking)
        {
            FocusedNoradId = "44909"
        };

        var snapshot = provider.GetCurrent();

        Assert.Equal("RS-44", snapshot.SatelliteName);
        Assert.False(snapshot.IsAvailable);
        Assert.Equal(0, snapshot.UplinkHz);
        Assert.Equal(0, snapshot.DownlinkHz);
    }

    [Fact]
    public void GetCurrent_uses_focused_track_name_and_freqs_when_overlay_is_synced()
    {
        var settings = new TestSettingsService();
        var database = new TestSatelliteDatabaseService(
        [
            CreateJo97Entry(),
            CreateRs44Entry()
        ]);

        var frequencies = new FrequencyOverlayViewModel(settings, database, LocalizationService.Instance);
        frequencies.Update(CreateTrack("JO-97", "43855"));
        frequencies.Update(CreateTrack("RS-44", "44909"));

        var liveTracking = new StubLiveTrackingService(
        [
            CreateTrack("JO-97", "43855"),
            CreateTrack("RS-44", "44909")
        ]);

        var provider = new LiveTrackerSnapshotProvider(frequencies, liveTracking)
        {
            FocusedNoradId = "44909"
        };

        var snapshot = provider.GetCurrent();

        Assert.Equal("RS-44", snapshot.SatelliteName);
        Assert.True(snapshot.IsAvailable);
        Assert.True(snapshot.UplinkHz > 0 || snapshot.DownlinkHz > 0);
    }

    private static SatelliteTrackState CreateTrack(string name, string noradId) =>
        new()
        {
            Name = name,
            NoradId = noradId,
            Subpoint = new GeoCoordinate(57, 18),
            LookAngles = new LookAngles(180, 25, 800, 2.5)
        };

    private static SatelliteRadioEntry CreateJo97Entry() =>
        new()
        {
            Name = "JO-97",
            NoradId = "43855",
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
        };

    private static SatelliteRadioEntry CreateRs44Entry() =>
        new()
        {
            Name = "RS-44",
            NoradId = "44909",
            Modes =
            [
                new SatelliteTransponderMode
                {
                    Type = "SSB Transponder",
                    DownlinkKHz = 435_640,
                    UplinkKHz = 145_965,
                    DownlinkMode = "USB",
                    UplinkMode = "LSB",
                    Doppler = "REV"
                }
            ]
        };

    private sealed class StubLiveTrackingService(IReadOnlyList<SatelliteTrackState> states) : ILiveTrackingService
    {
        public DateTime SnapshotUtc => DateTime.UtcNow;
        public TimeSpan MapTimeOffset { get; set; }
        public string? FocusedNoradId { get; set; }
        public DateTime LiveNowSnapshotUtc => DateTime.UtcNow;

        public IReadOnlyList<SatelliteTrackState> GetSnapshot() => states;
        public IReadOnlyList<SatelliteTrackState> GetLiveNowSnapshot() => states;
        public void Start() { }
        public void RequestReload() { }
        public SnapshotBufferStatistics GetBufferStatistics() => new SnapshotBufferStatistics();
        public void CompactBuffers() { }
        public void Dispose() { }
    }

    private sealed class TestSettingsService : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public string? LoadError => null;
        public bool CanPersist => true;
        public string SettingsPath { get; } = Path.Combine(Path.GetTempPath(), "oscarwatch-test-settings.json");
        public string SerializeCurrent() => "{}";
        public Task ReplaceAndSaveAsync(AppSettings imported, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Load() { }
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void RequestSave() { }
        public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
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

        public SatelliteRadioEntry? TryGetEntry(string satelliteName, string? noradId = null) =>
            Entries.FirstOrDefault(e => e.Name.Equals(satelliteName, StringComparison.OrdinalIgnoreCase))
            ?? (noradId is null
                ? null
                : Entries.FirstOrDefault(e =>
                    string.Equals(e.NoradId, noradId, StringComparison.OrdinalIgnoreCase)));

        public void Reload() { }
    }
}
