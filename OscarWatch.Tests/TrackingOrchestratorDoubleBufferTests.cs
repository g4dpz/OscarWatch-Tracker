using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

public sealed class TrackingOrchestratorDoubleBufferTests
{
    [Fact]
    public void Consecutive_GetLiveStates_calls_return_correct_data()
    {
        var satellites = new[]
        {
            SampleSatellite("ISS", "25544"),
            SampleSatellite("SO-50", "27607")
        };

        var orchestrator = CreateOrchestrator(satellites);
        orchestrator.ReloadEnabledSatellites();

        var utc1 = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var utc2 = new DateTime(2024, 6, 15, 12, 0, 1, DateTimeKind.Utc);

        var result1 = orchestrator.GetLiveStates(utc1);
        var result2 = orchestrator.GetLiveStates(utc2);

        // Both calls should return all satellites
        Assert.Equal(2, result2.Count);
        Assert.Contains(result2, s => s.NoradId == "25544");
        Assert.Contains(result2, s => s.NoradId == "27607");
    }

    [Fact]
    public void Previous_buffer_is_cleared_on_swap()
    {
        var satellites = new[]
        {
            SampleSatellite("ISS", "25544"),
            SampleSatellite("SO-50", "27607")
        };

        var orchestrator = CreateOrchestrator(satellites);
        orchestrator.ReloadEnabledSatellites();

        var utc = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        // Call 1: returns bufferB (since _useBufferA starts true, the inactive buffer is B)
        var result1 = orchestrator.GetLiveStates(utc);
        Assert.Equal(2, result1.Count);

        // Call 2: returns bufferA
        var result2 = orchestrator.GetLiveStates(utc.AddSeconds(1));
        Assert.Equal(2, result2.Count);

        // Call 3: selects bufferB again and clears it before populating.
        // Since result1 points to bufferB, the old reference is now cleared and refilled.
        var result3 = orchestrator.GetLiveStates(utc.AddSeconds(2));

        // result1 and result3 are the same reference (bufferB), proving the old buffer was reused.
        // The old data from result1 is gone — it now holds call 3's data.
        Assert.Same(result1, result3);

        // Verify result2 (bufferA) still holds its data until the next swap clears it
        Assert.Equal(2, result2.Count);

        // Call 4: selects bufferA, clears it — now result2's old data is gone
        var result4 = orchestrator.GetLiveStates(utc.AddSeconds(3));
        Assert.Same(result2, result4);
    }

    [Fact]
    public void No_new_list_allocation_after_initial_capacity_growth()
    {
        var satellites = new[]
        {
            SampleSatellite("ISS", "25544"),
            SampleSatellite("SO-50", "27607")
        };

        var orchestrator = CreateOrchestrator(satellites);
        orchestrator.ReloadEnabledSatellites();

        var utc = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        // Call 1: returns buffer B (inactive when _useBufferA starts true)
        var call1 = orchestrator.GetLiveStates(utc);
        // Call 2: returns buffer A
        var call2 = orchestrator.GetLiveStates(utc.AddSeconds(1));
        // Call 3: returns buffer B again — same reference as call 1
        var call3 = orchestrator.GetLiveStates(utc.AddSeconds(2));
        // Call 4: returns buffer A again — same reference as call 2
        var call4 = orchestrator.GetLiveStates(utc.AddSeconds(3));

        // The references should alternate between the same two objects,
        // proving no new List<> is allocated.
        Assert.Same(call1, call3);
        Assert.Same(call2, call4);
        Assert.NotSame(call1, call2);
    }

    private static TrackingOrchestrator CreateOrchestrator(IReadOnlyList<SatelliteCatalogEntry> satellites)
    {
        return new TrackingOrchestrator(
            new TestSettingsService(),
            new StubTleService(satellites),
            new MinimalPropagator(satellites),
            new StubGroundGeometry(),
            new NullPassPredictor());
    }

    private static SatelliteCatalogEntry SampleSatellite(string name, string noradId) => new()
    {
        Name = name,
        NoradId = noradId,
        Line1 = "1 25544U 98067A   24001.50000000  .00016717  00000-0  10270-3 0  9993",
        Line2 = "2 25544  51.6400 247.4627 0006703 130.5360 325.0288 15.49519779439320"
    };

    private sealed class StubGroundGeometry : Core.Orbit.IGroundGeometry
    {
        public IReadOnlyList<GeoCoordinate> GetGroundTrack(
            SatelliteCatalogEntry satellite,
            DateTime utcStart,
            DateTime utcEnd,
            TimeSpan step) => [];

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
        public string CachePath => Path.Combine(Path.GetTempPath(), "tle-double-buffer-test");
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
        public string SettingsPath { get; } = Path.Combine(Path.GetTempPath(), "double-buffer-test-settings.json");
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
