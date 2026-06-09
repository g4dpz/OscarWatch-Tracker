using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

public sealed class TrackingDiagnosticsTests
{
    [Fact]
    public void GetLiveStates_logs_look_angle_skip_once_per_norad()
    {
        var diagnostics = new RecordingTrackingDiagnostics();
        var propagator = new ThrowingLookAnglesPropagator();
        var orchestrator = new TrackingOrchestrator(
            new TestSettingsService(),
            new StubTleService([SampleSatellite()]),
            propagator,
            new NullGroundGeometry(),
            new NullPassPredictor(),
            diagnostics);

        orchestrator.ReloadEnabledSatellites();
        orchestrator.GetLiveStates(DateTime.UtcNow);
        orchestrator.GetLiveStates(DateTime.UtcNow);

        Assert.Single(diagnostics.LookAngleSkips);
        Assert.Equal("25544", diagnostics.LookAngleSkips[0].NoradId);
    }

    [Fact]
    public void ReloadEnabledSatellites_clears_logged_skip_cache()
    {
        var diagnostics = new RecordingTrackingDiagnostics();
        var propagator = new ThrowingLookAnglesPropagator();
        var orchestrator = new TrackingOrchestrator(
            new TestSettingsService(),
            new StubTleService([SampleSatellite()]),
            propagator,
            new NullGroundGeometry(),
            new NullPassPredictor(),
            diagnostics);

        orchestrator.ReloadEnabledSatellites();
        orchestrator.GetLiveStates(DateTime.UtcNow);
        orchestrator.ReloadEnabledSatellites();
        orchestrator.GetLiveStates(DateTime.UtcNow);

        Assert.Equal(2, diagnostics.LookAngleSkips.Count);
    }

    private static SatelliteCatalogEntry SampleSatellite() => new()
    {
        Name = "ISS",
        NoradId = "25544",
        Line1 = "1 25544U 98067A   24001.50000000  .00016717  00000-0  10270-3 0  9993",
        Line2 = "2 25544  51.6400 247.4627 0006703 130.5360 325.0288 15.49519779439320"
    };

    private sealed class RecordingTrackingDiagnostics : ITrackingDiagnostics
    {
        public List<(string NoradId, DateTime Utc, Exception Exception)> LookAngleSkips { get; } = [];

        public List<(string NoradId, DateTime Utc, Exception Exception)> StateSkips { get; } = [];

        public void LookAnglesSkipped(string noradId, DateTime utc, Exception exception) =>
            LookAngleSkips.Add((noradId, utc, exception));

        public void SatelliteStateSkipped(string noradId, DateTime utc, Exception exception) =>
            StateSkips.Add((noradId, utc, exception));
    }

    private sealed class ThrowingLookAnglesPropagator : Core.Orbit.IOrbitPropagator
    {
        public IReadOnlyCollection<string> LoadedNoradIds { get; } = ["25544"];

        public void Clear() { }
        public void LoadSatellite(SatelliteCatalogEntry entry) { }
        public void RemoveSatellite(string noradId) { }
        public bool HasSatellite(string noradId) => noradId == "25544";

        public GeoCoordinate GetSubpoint(string noradId, DateTime utc) =>
            new(0, 0, 400);

        public EciPosition GetEciPosition(string noradId, DateTime utc) =>
            new(0, 0, 0);

        public LookAngles GetLookAngles(string noradId, GroundStation site, DateTime utc) =>
            throw new InvalidOperationException("decayed");
    }

    private sealed class StubTleService(IReadOnlyList<SatelliteCatalogEntry> satellites) : ITleService
    {
        public IReadOnlyList<SatelliteCatalogEntry> Catalog => satellites;
        public DateTime? LastFetchedUtc => DateTime.UtcNow;
        public string CachePath => Path.Combine(Path.GetTempPath(), "tle-cache-test");
        public string ActiveSourceLabel => "test";
        public IReadOnlyList<SatelliteCatalogEntry> GetEnabledSatellites(AppSettings settings) => satellites;
        public Task EnsureLoadedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RefreshAsync(bool force = false, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void InvalidateCatalog() { }
        public bool IsStale(int staleHours) => false;
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

    private sealed class TestSettingsService : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public string SettingsPath { get; } = Path.Combine(Path.GetTempPath(), "tracking-diagnostics-test.json");
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
}
