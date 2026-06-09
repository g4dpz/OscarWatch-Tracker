using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

public sealed class TrackingOrchestratorGroundTrackTests
{
    [Fact]
    public void GetLiveStates_builds_ground_track_only_for_focused_norad()
    {
        var geometry = new CountingGroundGeometry();
        var satellites = new[]
        {
            SampleSatellite("ISS", "25544"),
            SampleSatellite("SO-50", "27607")
        };

        var orchestrator = new TrackingOrchestrator(
            new TestSettingsService(),
            new StubTleService(satellites),
            new MinimalPropagator(satellites),
            geometry,
            new NullPassPredictor());

        orchestrator.ReloadEnabledSatellites();

        var states = orchestrator.GetLiveStates(DateTime.UtcNow, groundTrackNoradId: "27607");

        Assert.Equal(2, states.Count);
        Assert.Empty(states.First(s => s.NoradId == "25544").GroundTrack);
        Assert.NotNull(states.First(s => s.NoradId == "25544").MotionHeadingDeg);
        Assert.Equal(2, states.First(s => s.NoradId == "27607").GroundTrack.Count);
        Assert.NotNull(states.First(s => s.NoradId == "27607").MotionHeadingDeg);
        Assert.Equal(1, geometry.GroundTrackCallCount);
    }

    [Fact]
    public void GetLiveStates_skips_ground_track_when_focus_is_unset()
    {
        var geometry = new CountingGroundGeometry();
        var satellites = new[] { SampleSatellite("ISS", "25544") };
        var orchestrator = new TrackingOrchestrator(
            new TestSettingsService(),
            new StubTleService(satellites),
            new MinimalPropagator(satellites),
            geometry,
            new NullPassPredictor());

        orchestrator.ReloadEnabledSatellites();
        var states = orchestrator.GetLiveStates(DateTime.UtcNow);

        Assert.Equal(0, geometry.GroundTrackCallCount);
        Assert.NotNull(states[0].MotionHeadingDeg);
    }

    private static SatelliteCatalogEntry SampleSatellite(string name, string noradId) => new()
    {
        Name = name,
        NoradId = noradId,
        Line1 = "1 25544U 98067A   24001.50000000  .00016717  00000-0  10270-3 0  9993",
        Line2 = "2 25544  51.6400 247.4627 0006703 130.5360 325.0288 15.49519779439320"
    };

    private sealed class CountingGroundGeometry : Core.Orbit.IGroundGeometry
    {
        public int GroundTrackCallCount { get; private set; }

        public IReadOnlyList<GeoCoordinate> GetGroundTrack(
            SatelliteCatalogEntry satellite,
            DateTime utcStart,
            DateTime utcEnd,
            TimeSpan step)
        {
            GroundTrackCallCount++;
            return
            [
                new GeoCoordinate(0, 0, 400),
                new GeoCoordinate(1, 1, 400)
            ];
        }

        public IReadOnlyList<GeoCoordinate> GetFootprint(
            SatelliteCatalogEntry satellite,
            DateTime utc,
            double minimumElevationDeg) =>
        [
            new(0, 0, 0),
            new(1, 0, 0),
            new(0, 1, 0)
        ];
    }

    private sealed class MinimalPropagator(IReadOnlyList<SatelliteCatalogEntry> satellites) : Core.Orbit.IOrbitPropagator
    {
        private readonly HashSet<string> _ids = satellites.Select(s => s.NoradId).ToHashSet(StringComparer.Ordinal);

        public IReadOnlyCollection<string> LoadedNoradIds => _ids;

        public void Clear() => _ids.Clear();
        public void LoadSatellite(SatelliteCatalogEntry entry) => _ids.Add(entry.NoradId);
        public void RemoveSatellite(string noradId) => _ids.Remove(noradId);
        public bool HasSatellite(string noradId) => _ids.Contains(noradId);
        public GeoCoordinate GetSubpoint(string noradId, DateTime utc) =>
            new(utc.Second * 0.01, utc.Second * 0.01, 400);
        public EciPosition GetEciPosition(string noradId, DateTime utc) => new(7000, 0, 0);
        public LookAngles GetLookAngles(string noradId, GroundStation site, DateTime utc) =>
            new(180, 45, 1000, 0);
    }

    private sealed class StubTleService(IReadOnlyList<SatelliteCatalogEntry> satellites) : ITleService
    {
        public IReadOnlyList<SatelliteCatalogEntry> Catalog => satellites;
        public DateTime? LastFetchedUtc => DateTime.UtcNow;
        public string CachePath => Path.Combine(Path.GetTempPath(), "tle-ground-track-test");
        public string ActiveSourceLabel => "test";
        public IReadOnlyList<SatelliteCatalogEntry> GetEnabledSatellites(AppSettings settings) => satellites;
        public Task EnsureLoadedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RefreshAsync(bool force = false, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void InvalidateCatalog() { }
        public bool IsStale(int staleHours) => false;
    }

    private sealed class TestSettingsService : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public string SettingsPath { get; } = Path.Combine(Path.GetTempPath(), "ground-track-test-settings.json");
        public string SerializeCurrent() => "{}";
        public Task ReplaceAndSaveAsync(AppSettings imported, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Load() { }
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void RequestSave() { }
        public void SyncGridFromLatLon() { }
        public void SyncLatLonFromGrid() { }
        public void EnsureSavedStations() { }
        public void ApplyActiveStation() { }
        public void SyncActiveStationFromGroundStation() { }
    }

    private sealed class NullPassPredictor : Core.Orbit.IPassPredictor
    {
        public Task<IReadOnlyList<PassInfo>> GetPassesAsync(
            SatelliteCatalogEntry satellite,
            GroundStation site,
            DateTime utcStart,
            DateTime utcEnd,
            double minimumElevationDeg,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PassInfo>>([]);
    }
}
