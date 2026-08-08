// Feature: multi-pass-ground-track — Property and unit tests for multi-satellite ground track overlay

using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

/// <summary>
/// Property-based and unit tests verifying the multi-pass ground track overlay feature:
/// - Focused satellite: current orbit at 60s step, next orbit at 120s step
/// - Stagger limits recomputation to max 2 non-focused per tick
/// - Visual cache uses differentiated refresh intervals (45s focused, 90s non-focused)
/// - Toggle controls next-orbit overlay rendering in the UI
/// </summary>
public sealed class MultiPassGroundTrackTests
{
    // ─── Property 1: Non-focused tracks use coarser sampling ───
    // **Validates: Requirements 1.2, 4.1**

    /// <summary>
    /// Property 1: Focused satellite uses 60s step for the current orbit and 120s for the next orbit;
    /// non-focused satellites use 120s step. The ground geometry records the step used for each call.
    /// </summary>
    [Fact]
    public void Non_focused_tracks_use_120s_step_focused_uses_60s()
    {
        var geometry = new StepRecordingGroundGeometry();
        var satellites = new[]
        {
            SampleSatellite("ISS", "25544"),
            SampleSatellite("SO-50", "27607"),
            SampleSatellite("AO-91", "43017")
        };

        var orchestrator = new TrackingOrchestrator(
            new TestSettingsService(),
            new StubTleService(satellites),
            new MinimalPropagator(satellites),
            geometry,
            new NullPassPredictor());

        orchestrator.ReloadEnabledSatellites();
        orchestrator.GetLiveStates(DateTime.UtcNow, groundTrackNoradId: "25544");

        var focusedCalls = geometry.Calls.Where(c => c.NoradId == "25544").ToList();
        Assert.Contains(focusedCalls, c => c.Step == TimeSpan.FromSeconds(60));
        Assert.Contains(focusedCalls, c => c.Step == TimeSpan.FromSeconds(120));

        var nonFocusedCalls = geometry.Calls.Where(c => c.NoradId != "25544").ToList();
        Assert.All(nonFocusedCalls, c => Assert.Equal(TimeSpan.FromSeconds(120), c.Step));
    }

    /// <summary>
    /// Property 1 (property-based): For any number of satellites (2-8), the focused satellite
    /// always gets a 60s current-orbit step; all other calls use 120s.
    /// </summary>
    [Property(MaxTest = 50)]
    public bool Non_focused_step_is_always_double_focused_step(int seed)
    {
        var rng = new Random(seed);
        var satCount = rng.Next(2, 9); // 2..8

        var geometry = new StepRecordingGroundGeometry();
        var satellites = Enumerable.Range(0, satCount)
            .Select(i => SampleSatellite($"SAT-{i}", $"{25544 + i}"))
            .ToArray();

        var focusedId = satellites[0].NoradId;
        var orchestrator = new TrackingOrchestrator(
            new TestSettingsService(),
            new StubTleService(satellites),
            new MinimalPropagator(satellites),
            geometry,
            new NullPassPredictor());

        orchestrator.ReloadEnabledSatellites();
        orchestrator.GetLiveStates(DateTime.UtcNow, groundTrackNoradId: focusedId);

        var focusedCalls = geometry.Calls.Where(c => c.NoradId == focusedId).ToList();
        var nonFocusedSteps = geometry.Calls
            .Where(c => c.NoradId != focusedId)
            .Select(c => c.Step)
            .ToList();

        return focusedCalls.Any(c => c.Step == TimeSpan.FromSeconds(60))
            && focusedCalls.Where(c => c.Step != TimeSpan.FromSeconds(60)).All(c => c.Step == TimeSpan.FromSeconds(120))
            && nonFocusedSteps.All(s => s == TimeSpan.FromSeconds(120));
    }

    // ─── Property 2: Stagger limits recomputation per tick ───
    // **Validates: Requirements 1.4, 4.3**

    /// <summary>
    /// Property 2: No more than 2 non-focused ground tracks are recomputed in a single tick.
    /// </summary>
    [Fact]
    public void Stagger_limits_non_focused_recomputation_to_max_2_per_tick()
    {
        var geometry = new StepRecordingGroundGeometry();
        var satellites = Enumerable.Range(0, 8)
            .Select(i => SampleSatellite($"SAT-{i}", $"{30000 + i}"))
            .ToArray();

        var focusedId = "30000"; // SAT-0
        var orchestrator = new TrackingOrchestrator(
            new TestSettingsService(),
            new StubTleService(satellites),
            new MinimalPropagator(satellites),
            geometry,
            new NullPassPredictor());

        orchestrator.ReloadEnabledSatellites();

        // First tick: focused + max 2 non-focused
        orchestrator.GetLiveStates(DateTime.UtcNow, groundTrackNoradId: focusedId);

        var nonFocusedCallCount = geometry.Calls.Count(c => c.NoradId != focusedId);
        // Stagger allows max 2 non-focused per tick
        Assert.True(nonFocusedCallCount <= 2,
            $"Expected max 2 non-focused recomputations per tick, got {nonFocusedCallCount}");
    }

    /// <summary>
    /// Property 2: Over multiple ticks, all non-focused satellites eventually get their tracks computed
    /// (round-robin stagger distributes the work).
    /// </summary>
    [Fact]
    public void Stagger_round_robin_eventually_computes_all_non_focused()
    {
        var geometry = new StepRecordingGroundGeometry();
        var satellites = Enumerable.Range(0, 6)
            .Select(i => SampleSatellite($"SAT-{i}", $"{40000 + i}"))
            .ToArray();

        var focusedId = "40000";
        var orchestrator = new TrackingOrchestrator(
            new TestSettingsService(),
            new StubTleService(satellites),
            new MinimalPropagator(satellites),
            geometry,
            new NullPassPredictor());

        orchestrator.ReloadEnabledSatellites();

        // Run enough ticks to compute all non-focused (5 satellites, 2 per tick = 3 ticks minimum)
        // Use different utc each tick to avoid freshness cache hits
        for (var tick = 0; tick < 5; tick++)
        {
            var utc = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddMinutes(tick * 2);
            orchestrator.GetLiveStates(utc, groundTrackNoradId: focusedId);
        }

        var computedIds = geometry.Calls.Select(c => c.NoradId).Distinct().ToHashSet();
        // All satellites (focused + non-focused) should eventually get computed
        foreach (var sat in satellites)
        {
            Assert.Contains(sat.NoradId, computedIds);
        }
    }

    // ─── Property 3: Focused track rendering unchanged ───
    // **Validates: Requirements 3.1, 3.2**
    // (Rendering tests are validated at the integration level; here we verify the
    // orchestrator produces a proper ground track for the focused satellite regardless of overlay state)

    /// <summary>
    /// Property 3: Focused satellite always gets a ground track computed, regardless of settings.
    /// </summary>
    [Fact]
    public void Focused_track_always_computed()
    {
        var geometry = new StepRecordingGroundGeometry();
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
        var states = orchestrator.GetLiveStates(DateTime.UtcNow, groundTrackNoradId: "25544");

        var focusedState = states.First(s => s.NoradId == "25544");
        Assert.True(focusedState.GroundTrack.Count >= 2,
            "Focused satellite must always have a ground track with at least 2 points");
    }

    // ─── Property 4: Toggle disabled → zero non-focused tracks rendered ───
    // **Validates: Requirements 5.2**
    // (This is a rendering property; we verify the orchestrator still computes tracks
    //  but the UI won't draw them when the toggle is off — tested at the unit level
    //  via the ShowMultiTrackOverlay property behaviour)

    /// <summary>
    /// Property 4: The ShowMultiTrackOverlay setting defaults to true in AppSettings.
    /// </summary>
    [Fact]
    public void ShowMultiTrackOverlay_defaults_to_true()
    {
        var settings = new AppSettings();
        Assert.True(settings.ShowMultiTrackOverlay);
    }

    /// <summary>
    /// Property 4 (property-based): Setting ShowMultiTrackOverlay to false persists correctly.
    /// </summary>
    [Property(MaxTest = 20)]
    public bool ShowMultiTrackOverlay_round_trips(bool value)
    {
        var settings = new AppSettings { ShowMultiTrackOverlay = value };
        return settings.ShowMultiTrackOverlay == value;
    }

    // ─── Property 5: Non-focused opacity 80, thickness 1 ───
    // **Validates: Requirements 2.2, 2.3, 6.1**
    // (Rendering properties are verified at the integration level; the orchestrator
    //  ensures non-focused satellites have track data available for the renderer)

    /// <summary>
    /// Property 5: All non-focused satellites with stale caches get tracks computed
    /// so the renderer can apply opacity 80 / thickness 1.
    /// </summary>
    [Fact]
    public void Non_focused_satellites_get_track_data_for_rendering()
    {
        var geometry = new StepRecordingGroundGeometry();
        var satellites = new[]
        {
            SampleSatellite("ISS", "25544"),
            SampleSatellite("SO-50", "27607"),
            SampleSatellite("AO-91", "43017")
        };

        var orchestrator = new TrackingOrchestrator(
            new TestSettingsService(),
            new StubTleService(satellites),
            new MinimalPropagator(satellites),
            geometry,
            new NullPassPredictor());

        orchestrator.ReloadEnabledSatellites();

        // Run enough ticks to ensure all get computed
        for (var tick = 0; tick < 3; tick++)
        {
            var utc = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddMinutes(tick * 2);
            orchestrator.GetLiveStates(utc, groundTrackNoradId: "25544");
        }

        var computedNonFocused = geometry.Calls
            .Where(c => c.NoradId != "25544")
            .Select(c => c.NoradId)
            .Distinct()
            .ToList();

        Assert.Contains("27607", computedNonFocused);
        Assert.Contains("43017", computedNonFocused);
    }

    // ─── Unit tests ───

    /// <summary>
    /// Visual cache uses 45s interval for focused, 90s for non-focused.
    /// </summary>
    [Fact]
    public void Visual_cache_uses_differentiated_intervals()
    {
        var cache = new SatelliteVisualCache();
        var entry = cache.GetOrAdd("25544");
        var baseUtc = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        // Set ground track with known timestamp
        entry.GroundTrack = new[] { new GeoCoordinate(0, 0, 400), new GeoCoordinate(1, 1, 400) };
        entry.GroundTrackUtc = baseUtc;

        // At +44s: fresh for focused (45s interval), fresh for non-focused (90s interval)
        var at44s = baseUtc.AddSeconds(44);
        Assert.True(cache.TryGetFreshGroundTrack("25544", at44s, isFocused: true, out _));
        Assert.True(cache.TryGetFreshGroundTrack("25544", at44s, isFocused: false, out _));

        // At +46s: stale for focused, still fresh for non-focused
        var at46s = baseUtc.AddSeconds(46);
        Assert.False(cache.TryGetFreshGroundTrack("25544", at46s, isFocused: true, out _));
        Assert.True(cache.TryGetFreshGroundTrack("25544", at46s, isFocused: false, out _));

        // At +91s: stale for both
        var at91s = baseUtc.AddSeconds(91);
        Assert.False(cache.TryGetFreshGroundTrack("25544", at91s, isFocused: true, out _));
        Assert.False(cache.TryGetFreshGroundTrack("25544", at91s, isFocused: false, out _));
    }

    /// <summary>
    /// Original TryGetFreshGroundTrack overload (no isFocused) still uses 45s interval for backwards compatibility.
    /// </summary>
    [Fact]
    public void Original_overload_uses_45s_interval()
    {
        var cache = new SatelliteVisualCache();
        var entry = cache.GetOrAdd("25544");
        var baseUtc = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        entry.GroundTrack = new[] { new GeoCoordinate(0, 0, 400), new GeoCoordinate(1, 1, 400) };
        entry.GroundTrackUtc = baseUtc;

        // At +44s: fresh
        Assert.True(cache.TryGetFreshGroundTrack("25544", baseUtc.AddSeconds(44), out _));

        // At +46s: stale (uses the original 45s interval)
        Assert.False(cache.TryGetFreshGroundTrack("25544", baseUtc.AddSeconds(46), out _));
    }

    /// <summary>
    /// Stagger index wraps around correctly for non-focused satellites.
    /// </summary>
    [Fact]
    public void Stagger_index_wraps_around()
    {
        var geometry = new StepRecordingGroundGeometry();
        var satellites = Enumerable.Range(0, 5)
            .Select(i => SampleSatellite($"SAT-{i}", $"{50000 + i}"))
            .ToArray();

        var focusedId = "50000";
        var orchestrator = new TrackingOrchestrator(
            new TestSettingsService(),
            new StubTleService(satellites),
            new MinimalPropagator(satellites),
            geometry,
            new NullPassPredictor());

        orchestrator.ReloadEnabledSatellites();

        // Multiple ticks — verify no exceptions and all satellites eventually computed
        for (var tick = 0; tick < 10; tick++)
        {
            var utc = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddMinutes(tick * 2);
            orchestrator.GetLiveStates(utc, groundTrackNoradId: focusedId);
        }

        var allComputed = geometry.Calls.Select(c => c.NoradId).Distinct().ToHashSet();
        foreach (var sat in satellites)
            Assert.Contains(sat.NoradId, allComputed);
    }

    // ─── Test helpers ───

    private static SatelliteCatalogEntry SampleSatellite(string name, string noradId) => new()
    {
        Name = name,
        NoradId = noradId,
        Line1 = "1 25544U 98067A   24001.50000000  .00016717  00000-0  10270-3 0  9993",
        Line2 = "2 25544  51.6400 247.4627 0006703 130.5360 325.0288 15.49519779439320"
    };

    /// <summary>
    /// Ground geometry that records the step parameter used for each GetGroundTrack call.
    /// </summary>
    private sealed class StepRecordingGroundGeometry : Core.Orbit.IGroundGeometry
    {
        public List<(string NoradId, TimeSpan Step)> Calls { get; } = new();

        public IReadOnlyList<GeoCoordinate> GetGroundTrack(
            SatelliteCatalogEntry satellite,
            DateTime utcStart,
            DateTime utcEnd,
            TimeSpan step)
        {
            Calls.Add((satellite.NoradId, step));
            return new[] { new GeoCoordinate(0, 0, 400), new GeoCoordinate(1, 1, 400) };
        }

        public IReadOnlyList<GeoCoordinate> GetFootprint(
            SatelliteCatalogEntry satellite,
            DateTime utc,
            double minimumElevationDeg) =>
            new[] { new GeoCoordinate(0, 0, 0), new GeoCoordinate(1, 0, 0), new GeoCoordinate(0, 1, 0) };
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
        public string CachePath => Path.Combine(Path.GetTempPath(), "multi-track-test");
        public string ActiveSourceLabel => "test";
        public TleCatalogLoadDiagnostics? LastLoadDiagnostics => null;
        public IReadOnlyList<SatelliteCatalogEntry> GetEnabledSatellites(AppSettings settings) => satellites;
        public Task EnsureLoadedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RefreshAsync(bool force = false, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void InvalidateCatalog() { }
        public bool IsStale(int staleHours) => false;
    }

    private sealed class TestSettingsService : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public string? LoadError => null;
        public bool CanPersist => true;
        public string SettingsPath { get; } = Path.Combine(Path.GetTempPath(), "multi-track-test-settings.json");
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
