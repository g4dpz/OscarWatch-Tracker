using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

public sealed class LiveTrackingServiceTests
{
    [Fact]
    public void Background_loop_updates_snapshot()
    {
        var calls = 0;
        var orchestrator = CreateMinimalOrchestrator();
        using var service = new LiveTrackingService(orchestrator, gps: null, _ =>
        {
            calls++;
            return
            [
                new SatelliteTrackState
                {
                    Name = "TEST",
                    NoradId = "1",
                    Subpoint = new GeoCoordinate(0, 0, 400)
                }
            ];
        });

        service.Start();
        service.RefreshSnapshotSynchronously();
        Assert.True(calls >= 1);
        Assert.Single(service.GetSnapshot());
        Assert.True(service.SnapshotUtc > DateTime.MinValue);
    }

    [Fact]
    public void RequestReload_refreshes_snapshot_on_worker()
    {
        var orchestrator = CreateMinimalOrchestrator();
        using var service = new LiveTrackingService(orchestrator, gps: null, _ => []);
        service.Start();
        service.RequestReload();
        service.DrainCommandQueueForTests();
        Assert.True(service.SnapshotUtc >= DateTime.MinValue);
    }

    [Fact]
    public void MapTimeOffset_shifts_snapshot_utc()
    {
        DateTime? capturedUtc = null;
        var orchestrator = CreateMinimalOrchestrator();
        using var service = new LiveTrackingService(orchestrator, gps: null, utc =>
        {
            capturedUtc = utc;
            return [];
        });

        service.Start();
        var before = DateTime.UtcNow;
        service.MapTimeOffset = TimeSpan.FromMinutes(30);
        service.RefreshSnapshotSynchronously();
        var after = DateTime.UtcNow;

        Assert.NotNull(capturedUtc);
        Assert.InRange(capturedUtc.Value, before.AddMinutes(30), after.AddMinutes(30).AddSeconds(1));
    }

    [Fact]
    public void GetSnapshot_is_safe_while_worker_updates()
    {
        var orchestrator = CreateMinimalOrchestrator();
        using var service = new LiveTrackingService(orchestrator, gps: null, utc =>
        [
            new SatelliteTrackState
            {
                Name = "TEST",
                NoradId = utc.Second.ToString(),
                Subpoint = new GeoCoordinate(0, 0, 400)
            }
        ]);

        service.Start();
        for (var i = 0; i < 20; i++)
        {
            _ = service.GetSnapshot();
            Thread.Sleep(15);
        }

        service.DrainCommandQueueForTests();
        service.Dispose();
    }

    private static TrackingOrchestrator CreateMinimalOrchestrator()
    {
        var settings = new TestSettingsService();
        var tle = new TleService();
        tle.EnsureLoadedAsync().GetAwaiter().GetResult();
        return new TrackingOrchestrator(
            settings,
            tle,
            new NullOrbitPropagator(),
            new NullGroundGeometry(),
            new NullPassPredictor());
    }

    private sealed class TestSettingsService : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public string SettingsPath { get; } = Path.Combine(Path.GetTempPath(), "oscarwatch-live-tracking-test.json");
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

    private sealed class NullOrbitPropagator : Core.Orbit.IOrbitPropagator
    {
        public IReadOnlyCollection<string> LoadedNoradIds { get; } = [];
        public void Clear() { }
        public void LoadSatellite(SatelliteCatalogEntry entry) { }
        public void RemoveSatellite(string noradId) { }
        public bool HasSatellite(string noradId) => false;
        public GeoCoordinate GetSubpoint(string noradId, DateTime utc) => new(0, 0, 0);
        public EciPosition GetEciPosition(string noradId, DateTime utc) => new(0, 0, 0);
        public LookAngles GetLookAngles(string noradId, GroundStation site, DateTime utc) =>
            new(0, 0, 0, 0);
    }

    private sealed class NullGroundGeometry : Core.Orbit.IGroundGeometry
    {
        public IReadOnlyList<GeoCoordinate> GetGroundTrack(
            SatelliteCatalogEntry satellite,
            DateTime utcStart,
            DateTime utcEnd,
            TimeSpan step) => [];

        public IReadOnlyList<GeoCoordinate> GetFootprint(
            SatelliteCatalogEntry satellite,
            DateTime utc,
            double minimumElevationDeg) => [];
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
