// Feature: performance-optimisations, Property 2: Enabled-satellite cache stability between reloads

using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 2.3, 2.5**
///
/// Property-based tests verifying that between two <see cref="TrackingOrchestrator.ReloadEnabledSatellites"/>
/// calls, multiple <see cref="TrackingOrchestrator.GetLiveStates"/> calls always operate on the same
/// satellite set — no spurious additions or deletions.
/// </summary>
public class EnabledSatelliteCachePropertyTests
{
    /// <summary>
    /// A pool of real satellite catalog entries with valid TLE data.
    /// </summary>
    private static readonly SatelliteCatalogEntry[] SatellitePool =
    [
        new()
        {
            Name = "ISS (ZARYA)", NoradId = "25544",
            Line1 = "1 25544U 98067A   26141.16510469  .00005835  00000-0  11282-3 0  9994",
            Line2 = "2 25544  51.6328  73.8715 0007529  81.3651 278.8190 15.49291753567565"
        },
        new()
        {
            Name = "AO-07", NoradId = "07530",
            Line1 = "1 07530U 74089B   26141.31992461 -.00000054  00000-0 -48931-4 0  9992",
            Line2 = "2 07530 101.9910 154.2858 0012269 180.6108 191.1977 12.53697584357151"
        },
        new()
        {
            Name = "AO-27", NoradId = "22825",
            Line1 = "1 22825U 93061C   26141.14902361  .00000060  00000-0  39806-4 0  9994",
            Line2 = "2 22825  98.6890 208.5706 0008550 172.0697 188.0622 14.30933961703139"
        },
        new()
        {
            Name = "FO-29", NoradId = "24278",
            Line1 = "1 24278U 96046B   26141.17662052  .00000000  00000-0  34829-4 0  9991",
            Line2 = "2 24278  98.5266 353.7450 0350115 166.3802 194.7089 13.53272915469510"
        },
        new()
        {
            Name = "SO-50", NoradId = "27607",
            Line1 = "1 27607U 02058C   26141.24923057  .00000576  00000-0  85866-4 0  9998",
            Line2 = "2 27607  64.5520 212.3264 0075596 267.4106  91.8345 14.82983020260469"
        },
        new()
        {
            Name = "AO-73", NoradId = "39444",
            Line1 = "1 39444U 13066AE  26140.67569056  .00005251  00000-0  33102-3 0  9992",
            Line2 = "2 39444  97.8265 111.5579 0034836 298.9376  60.8360 15.09093359675511"
        },
        new()
        {
            Name = "IO-86", NoradId = "40931",
            Line1 = "1 40931U 15052B   25151.18580175  .00001241  00000-0  78118-4 0  9996",
            Line2 = "2 40931   6.0006  24.0987 0012733 338.8432  21.1169 14.78805930523159"
        },
        new()
        {
            Name = "AO-91", NoradId = "43017",
            Line1 = "1 43017U 17073E   26141.14920854  .00006846  00000-0  30040-3 0  9994",
            Line2 = "2 43017  97.4737   8.9239 0153707  62.3580 299.3158 15.12168292461300"
        },
    ];

    /// <summary>
    /// Property 2: Enabled-satellite cache stability between reloads.
    ///
    /// Tests the invariant: between two ReloadEnabledSatellites calls, multiple GetLiveStates
    /// calls should always operate on the same satellite set (as observed via LoadedNoradIds
    /// on the propagator, which reflects what was loaded during reload).
    ///
    /// Strategy:
    /// - Pick an arbitrary subset of satellites to enable (via subsetMask).
    /// - Create a TrackingOrchestrator with a mock ITleService returning that subset.
    /// - Call ReloadEnabledSatellites once.
    /// - Call GetLiveStates N times (getLiveStatesCalls, 1-10).
    /// - Assert LoadedNoradIds stays the same set on every call.
    /// </summary>
    [Property]
    public bool Cache_is_stable_between_reloads(
        bool[] subsetMask,
        byte getLiveStatesCallsByte)
    {
        if (subsetMask is null || subsetMask.Length == 0)
            return true; // trivially true for empty inputs

        // Build the enabled satellite list from the subset mask
        var enabledSats = new List<SatelliteCatalogEntry>();
        for (var i = 0; i < subsetMask.Length && i < SatellitePool.Length; i++)
        {
            if (subsetMask[i])
                enabledSats.Add(SatellitePool[i]);
        }

        if (enabledSats.Count == 0)
            return true; // nothing to test with no enabled sats

        // Map the byte to a call count of 1-10
        var getLiveStatesCalls = (getLiveStatesCallsByte % 10) + 1;

        // Set up the orchestrator with a mock TLE service that returns our fixed list
        var settings = new StubSettingsService();
        var tleService = new StubTleService(enabledSats);
        var propagator = new TrackingPropagator();
        var groundGeometry = new StubGroundGeometry();
        var passPredictor = new StubPassPredictor();

        var orchestrator = new TrackingOrchestrator(
            settings, tleService, propagator, groundGeometry, passPredictor);

        // Perform a single reload — this populates the cache
        orchestrator.ReloadEnabledSatellites();

        // Capture the initial set of loaded NORAD IDs
        var expectedIds = new HashSet<string>(propagator.LoadedNoradIds, StringComparer.Ordinal);

        // Call GetLiveStates multiple times; assert that the propagator's loaded set
        // (which reflects what _cachedEnabledSats contains) never changes
        var utc = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < getLiveStatesCalls; i++)
        {
            var states = orchestrator.GetLiveStates(utc.AddSeconds(i));

            // The propagator's loaded set should not have changed
            var currentIds = new HashSet<string>(propagator.LoadedNoradIds, StringComparer.Ordinal);
            if (!currentIds.SetEquals(expectedIds))
                return false;

            // The returned states should only contain satellites from the enabled set
            foreach (var state in states)
            {
                if (!expectedIds.Contains(state.NoradId))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Property 2b: After a reload, GetLiveStates returns the same NORAD IDs
    /// regardless of how many times it is called (idempotence of cache reads).
    ///
    /// This is a stronger form: we check not just the propagator state but
    /// the actual NORAD IDs returned in the track states.
    /// </summary>
    [Property]
    public bool GetLiveStates_returns_consistent_norad_ids_between_reloads(
        bool[] subsetMask,
        byte callCountByte)
    {
        if (subsetMask is null || subsetMask.Length == 0)
            return true;

        var enabledSats = new List<SatelliteCatalogEntry>();
        for (var i = 0; i < subsetMask.Length && i < SatellitePool.Length; i++)
        {
            if (subsetMask[i])
                enabledSats.Add(SatellitePool[i]);
        }

        if (enabledSats.Count == 0)
            return true;

        var callCount = (callCountByte % 10) + 1;

        var settings = new StubSettingsService();
        var tleService = new StubTleService(enabledSats);
        var propagator = new TrackingPropagator();
        var groundGeometry = new StubGroundGeometry();
        var passPredictor = new StubPassPredictor();

        var orchestrator = new TrackingOrchestrator(
            settings, tleService, propagator, groundGeometry, passPredictor);

        orchestrator.ReloadEnabledSatellites();

        var utc = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        // Get the NORAD IDs from the first call
        var firstStates = orchestrator.GetLiveStates(utc);
        var firstNoradIds = new HashSet<string>(
            firstStates.Select(s => s.NoradId), StringComparer.Ordinal);

        // All subsequent calls should return the same set of NORAD IDs
        for (var i = 1; i < callCount; i++)
        {
            var states = orchestrator.GetLiveStates(utc.AddSeconds(i));
            var currentNoradIds = new HashSet<string>(
                states.Select(s => s.NoradId), StringComparer.Ordinal);

            if (!currentNoradIds.SetEquals(firstNoradIds))
                return false;
        }

        return true;
    }

    #region Test Doubles

    private sealed class StubSettingsService : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public string SettingsPath { get; } = "";
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

    /// <summary>
    /// A stub TLE service that always returns a fixed list of enabled satellites,
    /// regardless of settings. This allows us to control exactly which satellites
    /// the orchestrator sees during the test.
    /// </summary>
    private sealed class StubTleService : ITleService
    {
        private readonly IReadOnlyList<SatelliteCatalogEntry> _enabled;

        public StubTleService(IReadOnlyList<SatelliteCatalogEntry> enabled)
        {
            _enabled = enabled;
        }

        public IReadOnlyList<SatelliteCatalogEntry> Catalog => _enabled;
        public DateTime? LastFetchedUtc => DateTime.UtcNow;
        public string CachePath => "";
        public bool IsStale(int staleHours) => false;
        public Task RefreshAsync(bool force = false, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureLoadedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void InvalidateCatalog() { }
        public string ActiveSourceLabel => "Test";
        public IReadOnlyList<SatelliteCatalogEntry> GetEnabledSatellites(AppSettings settings) => _enabled;
    }

    /// <summary>
    /// A propagator that tracks loaded satellites and returns simple subpoints/ECI positions
    /// without performing real SGP4 propagation. This keeps the test fast and focused
    /// on cache stability rather than orbital mechanics.
    /// </summary>
    private sealed class TrackingPropagator : IOrbitPropagator
    {
        private readonly Dictionary<string, SatelliteCatalogEntry> _loaded = new(StringComparer.Ordinal);

        public IReadOnlyCollection<string> LoadedNoradIds => _loaded.Keys;

        public void LoadSatellite(SatelliteCatalogEntry entry) => _loaded[entry.NoradId] = entry;
        public void RemoveSatellite(string noradId) => _loaded.Remove(noradId);
        public void Clear() => _loaded.Clear();
        public bool HasSatellite(string noradId) => _loaded.ContainsKey(noradId);

        public GeoCoordinate GetSubpoint(string noradId, DateTime utc) => new(0, 0, 400);
        public EciPosition GetEciPosition(string noradId, DateTime utc) => new(6778, 0, 0);
        public LookAngles GetLookAngles(string noradId, GroundStation site, DateTime utc) => new(180, 45, 1000, 0);
    }

    private sealed class StubGroundGeometry : IGroundGeometry
    {
        public IReadOnlyList<GeoCoordinate> GetGroundTrack(
            SatelliteCatalogEntry satellite, DateTime utcStart, DateTime utcEnd, TimeSpan step) => [];

        public IReadOnlyList<GeoCoordinate> GetFootprint(
            SatelliteCatalogEntry satellite, DateTime utc, double minimumElevationDeg) => [];
    }

    private sealed class StubPassPredictor : IPassPredictor
    {
        public Task<IReadOnlyList<PassInfo>> GetPassesAsync(
            SatelliteCatalogEntry satellite, GroundStation site,
            DateTime utcStart, DateTime utcEnd, double minimumElevationDeg,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PassInfo>>([]);
    }

    #endregion
}
