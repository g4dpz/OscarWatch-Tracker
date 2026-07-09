// Feature: startup-io-rendering-optimisation, Property 8: Incremental operations equivalent to bulk reload

using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests.Performance;

/// <summary>
/// Property 8: For any sequence of AddSatellite and RemoveSatellite operations yielding a final
/// enabled set E, the propagator's LoadedNoradIds SHALL be identical to those produced after a
/// fresh ReloadEnabledSatellites with the same set E.
///
/// **Validates: Requirements 6.1, 6.2, 6.4**
/// </summary>
public class TrackingOrchestratorIncrementalPropertyTests
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
    /// Property 8: Incremental add operations yield the same propagator state as bulk reload.
    ///
    /// Strategy:
    /// 1. Pick an arbitrary subset of satellites via a boolean mask.
    /// 2. Add them one-by-one using AddSatellite.
    /// 3. Verify the propagator's LoadedNoradIds matches a fresh ReloadEnabledSatellites with the same set.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Incremental_add_matches_bulk_reload(bool[] subsetMask)
    {
        if (subsetMask is null || subsetMask.Length == 0)
            return true;

        // Determine the final enabled set from the mask
        var enabledSats = new List<SatelliteCatalogEntry>();
        for (var i = 0; i < subsetMask.Length && i < SatellitePool.Length; i++)
        {
            if (subsetMask[i])
                enabledSats.Add(SatellitePool[i]);
        }

        if (enabledSats.Count == 0)
            return true;

        // --- Incremental path: add satellites one-by-one ---
        var incrementalSettings = new StubSettingsService();
        var incrementalTleService = new StubTleService(enabledSats);
        var incrementalPropagator = new TrackingPropagator();
        var incrementalOrchestrator = new TrackingOrchestrator(
            incrementalSettings, incrementalTleService, incrementalPropagator,
            new StubGroundGeometry(), new StubPassPredictor());

        foreach (var sat in enabledSats)
            incrementalOrchestrator.AddSatellite(sat);

        var incrementalIds = new HashSet<string>(incrementalPropagator.LoadedNoradIds, StringComparer.Ordinal);

        // --- Bulk path: use ReloadEnabledSatellites ---
        var bulkSettings = new StubSettingsService();
        var bulkTleService = new StubTleService(enabledSats);
        var bulkPropagator = new TrackingPropagator();
        var bulkOrchestrator = new TrackingOrchestrator(
            bulkSettings, bulkTleService, bulkPropagator,
            new StubGroundGeometry(), new StubPassPredictor());

        bulkOrchestrator.ReloadEnabledSatellites();

        var bulkIds = new HashSet<string>(bulkPropagator.LoadedNoradIds, StringComparer.Ordinal);

        // The two sets should be identical
        return incrementalIds.SetEquals(bulkIds);
    }

    /// <summary>
    /// Property 8b: Incremental add followed by remove yields the correct final state.
    ///
    /// Strategy:
    /// 1. Start with a full set of satellites loaded via AddSatellite.
    /// 2. Remove a subset using RemoveSatellite.
    /// 3. Verify the propagator's LoadedNoradIds matches a fresh ReloadEnabledSatellites
    ///    with only the remaining satellites.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Incremental_add_then_remove_matches_bulk_reload(bool[] addMask, bool[] removeMask)
    {
        if (addMask is null || addMask.Length == 0)
            return true;

        // Build the initial set to add
        var toAdd = new List<SatelliteCatalogEntry>();
        for (var i = 0; i < addMask.Length && i < SatellitePool.Length; i++)
        {
            if (addMask[i])
                toAdd.Add(SatellitePool[i]);
        }

        if (toAdd.Count == 0)
            return true;

        // Determine which to remove from the added set
        var toRemoveIds = new HashSet<string>(StringComparer.Ordinal);
        var effectiveRemoveMask = removeMask ?? [];
        for (var i = 0; i < effectiveRemoveMask.Length && i < toAdd.Count; i++)
        {
            if (effectiveRemoveMask[i])
                toRemoveIds.Add(toAdd[i].NoradId);
        }

        // --- Incremental path: add all, then remove some ---
        var incrementalPropagator = new TrackingPropagator();
        var incrementalOrchestrator = new TrackingOrchestrator(
            new StubSettingsService(), new StubTleService(toAdd), incrementalPropagator,
            new StubGroundGeometry(), new StubPassPredictor());

        foreach (var sat in toAdd)
            incrementalOrchestrator.AddSatellite(sat);

        foreach (var id in toRemoveIds)
            incrementalOrchestrator.RemoveSatellite(id);

        var incrementalIds = new HashSet<string>(incrementalPropagator.LoadedNoradIds, StringComparer.Ordinal);

        // --- Bulk path: reload with only the remaining satellites ---
        var remaining = toAdd.Where(s => !toRemoveIds.Contains(s.NoradId)).ToList();
        var bulkPropagator = new TrackingPropagator();
        var bulkOrchestrator = new TrackingOrchestrator(
            new StubSettingsService(), new StubTleService(remaining), bulkPropagator,
            new StubGroundGeometry(), new StubPassPredictor());

        bulkOrchestrator.ReloadEnabledSatellites();

        var bulkIds = new HashSet<string>(bulkPropagator.LoadedNoradIds, StringComparer.Ordinal);

        return incrementalIds.SetEquals(bulkIds);
    }

    /// <summary>
    /// Property 8c: AddSatellite is idempotent — adding the same satellite twice
    /// does not duplicate it in the propagator.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool AddSatellite_is_idempotent(byte indexByte)
    {
        var index = indexByte % SatellitePool.Length;
        var sat = SatellitePool[index];

        var propagator = new TrackingPropagator();
        var orchestrator = new TrackingOrchestrator(
            new StubSettingsService(), new StubTleService([sat]), propagator,
            new StubGroundGeometry(), new StubPassPredictor());

        orchestrator.AddSatellite(sat);
        orchestrator.AddSatellite(sat); // second add should be a no-op

        return propagator.LoadedNoradIds.Count == 1
            && propagator.LoadedNoradIds.Contains(sat.NoradId);
    }

    /// <summary>
    /// Property 8d: RemoveSatellite on a non-existent ID is a safe no-op.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool RemoveSatellite_nonexistent_is_noop(bool[] addMask, NonEmptyString rawFakeId)
    {
        if (addMask is null || addMask.Length == 0)
            return true;

        var toAdd = new List<SatelliteCatalogEntry>();
        for (var i = 0; i < addMask.Length && i < SatellitePool.Length; i++)
        {
            if (addMask[i])
                toAdd.Add(SatellitePool[i]);
        }

        if (toAdd.Count == 0)
            return true;

        var propagator = new TrackingPropagator();
        var orchestrator = new TrackingOrchestrator(
            new StubSettingsService(), new StubTleService(toAdd), propagator,
            new StubGroundGeometry(), new StubPassPredictor());

        foreach (var sat in toAdd)
            orchestrator.AddSatellite(sat);

        var beforeIds = new HashSet<string>(propagator.LoadedNoradIds, StringComparer.Ordinal);

        // Remove a fake ID that doesn't exist in the pool
        var fakeId = rawFakeId.Get + "_NONEXISTENT";
        orchestrator.RemoveSatellite(fakeId);

        var afterIds = new HashSet<string>(propagator.LoadedNoradIds, StringComparer.Ordinal);

        return beforeIds.SetEquals(afterIds);
    }

    #region Test Doubles

    private sealed class StubSettingsService : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public string SettingsPath { get; } = "";
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
        public TleCatalogLoadDiagnostics? LastLoadDiagnostics => null;
        public bool IsStale(int staleHours) => false;
        public Task RefreshAsync(bool force = false, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureLoadedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void InvalidateCatalog() { }
        public string ActiveSourceLabel => "Test";
        public IReadOnlyList<SatelliteCatalogEntry> GetEnabledSatellites(AppSettings settings) => _enabled;
    }

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
