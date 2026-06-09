// Feature: performance-optimisations, Property 1: Parallel pass prediction preserves sorted, filtered output

using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 1.2, 1.5**
///
/// Property-based tests verifying that <see cref="TrackingOrchestrator.GetPassesAsync"/>
/// always returns results sorted by AOS time, and that the count matches the total of
/// all predictions meeting the minimum duration filter.
/// </summary>
public class ParallelPassPredictionPropertyTests
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
            Line1 = "1 07530U 74089B   26141.31992461 -.00000054  00000-0  -48931-4 0  9992",
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
    /// Property 1: Parallel pass prediction preserves sorted, filtered output.
    ///
    /// Strategy:
    /// - Pick an arbitrary subset of satellites (1-8 via subsetMask).
    /// - For each satellite, generate a set of fake passes with random AOS times.
    /// - Create a <see cref="FakePassPredictor"/> that returns those passes.
    /// - Call <see cref="TrackingOrchestrator.GetPassesAsync"/>.
    /// - Assert: the returned list is sorted by AosUtc.
    /// - Assert: the returned count equals the total passes that meet the minimum duration filter.
    /// </summary>
    [Property]
    public bool Output_is_sorted_by_AOS_and_count_matches_filtered_total(
        bool[] subsetMask,
        byte[] aosOffsetMinutes,
        byte durationMinutesByte)
    {
        if (subsetMask is null || subsetMask.Length == 0)
            return true;
        if (aosOffsetMinutes is null || aosOffsetMinutes.Length == 0)
            return true;

        // Build the enabled satellite list from the subset mask (1-8 satellites)
        var enabledSats = new List<SatelliteCatalogEntry>();
        for (var i = 0; i < subsetMask.Length && i < SatellitePool.Length; i++)
        {
            if (subsetMask[i])
                enabledSats.Add(SatellitePool[i]);
        }

        if (enabledSats.Count == 0)
            return true; // trivially true for empty inputs

        // Map the duration byte to a minimum duration of 0-5 minutes
        var minimumDurationMinutes = durationMinutesByte % 6;

        // Build fake passes for each satellite using the AOS offsets
        var baseTime = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var passesBySatellite = new Dictionary<string, List<PassInfo>>();
        var aosIndex = 0;

        foreach (var sat in enabledSats)
        {
            var passes = new List<PassInfo>();
            // Each satellite gets 1-3 passes depending on available offsets
            var passCount = Math.Min(3, aosOffsetMinutes.Length - aosIndex);
            if (passCount <= 0) passCount = 1;

            for (var p = 0; p < passCount && aosIndex < aosOffsetMinutes.Length; p++)
            {
                var aosOffset = aosOffsetMinutes[aosIndex++];
                var aos = baseTime.AddMinutes(aosOffset);
                // Duration: 3-10 minutes based on offset value
                var durationMin = 3 + (aosOffset % 8);
                var los = aos.AddMinutes(durationMin);

                passes.Add(new PassInfo
                {
                    SatelliteName = sat.Name,
                    NoradId = sat.NoradId,
                    AosUtc = aos,
                    LosUtc = los,
                    MaxElevationDeg = 15.0 + (aosOffset % 60),
                    MaxElevationUtc = aos.AddMinutes(durationMin / 2.0),
                    AosAzimuthDeg = 90.0,
                    LosAzimuthDeg = 270.0
                });
            }

            passesBySatellite[sat.NoradId] = passes;
        }

        // Set up the orchestrator with the fake predictor
        var settings = new StubSettingsService();
        var tleService = new StubTleService(enabledSats);
        var propagator = new TrackingPropagator();
        var groundGeometry = new StubGroundGeometry();
        var passPredictor = new FakePassPredictor(passesBySatellite);

        var orchestrator = new TrackingOrchestrator(
            settings, tleService, propagator, groundGeometry, passPredictor);

        // Call GetPassesAsync
        var site = new GroundStation();
        var minDuration = TimeSpan.FromMinutes(minimumDurationMinutes);
        var result = orchestrator.GetPassesAsync(
            site,
            minimumElevationDeg: 5.0,
            predictionHours: 24,
            minimumDurationMinutes: minimumDurationMinutes,
            CancellationToken.None).GetAwaiter().GetResult();

        // Assert 1: output is sorted by AosUtc
        for (var i = 1; i < result.Count; i++)
        {
            if (result[i].AosUtc < result[i - 1].AosUtc)
                return false;
        }

        // Assert 2: count matches total of all predictions that meet the minimum duration filter
        var expectedCount = passesBySatellite.Values
            .SelectMany(p => p)
            .Count(p => p.Duration >= minDuration);

        if (result.Count != expectedCount)
            return false;

        return true;
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

    /// <summary>
    /// A fake pass predictor that returns pre-configured passes per satellite NORAD ID.
    /// This allows the property test to control exactly what passes are returned and
    /// verify that the orchestrator correctly aggregates, filters, and sorts them.
    /// </summary>
    private sealed class FakePassPredictor : IPassPredictor
    {
        private readonly Dictionary<string, List<PassInfo>> _passesBySatellite;

        public FakePassPredictor(Dictionary<string, List<PassInfo>> passesBySatellite)
        {
            _passesBySatellite = passesBySatellite;
        }

        public Task<IReadOnlyList<PassInfo>> GetPassesAsync(
            SatelliteCatalogEntry satellite, GroundStation site,
            DateTime utcStart, DateTime utcEnd, double minimumElevationDeg,
            CancellationToken cancellationToken = default)
        {
            if (_passesBySatellite.TryGetValue(satellite.NoradId, out var passes))
                return Task.FromResult<IReadOnlyList<PassInfo>>(passes);

            return Task.FromResult<IReadOnlyList<PassInfo>>([]);
        }
    }

    #endregion
}
