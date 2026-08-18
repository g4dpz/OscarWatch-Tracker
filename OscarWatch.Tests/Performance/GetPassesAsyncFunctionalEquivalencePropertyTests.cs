// Feature: linq-hotpath-optimization, Property 5: Functional Equivalence Under All Conditions

using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests.Performance;

/// <summary>
/// Property 5: Functional Equivalence Under All Conditions
/// For any valid input combination including edge cases (empty collections, null tasks, zero durations),
/// the optimized implementation SHALL return results equivalent to the original LINQ implementation 
/// and handle exceptions identically.
///
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4**
/// </summary>
public class GetPassesAsyncFunctionalEquivalencePropertyTests
{
    /// <summary>
    /// A pool of satellite catalog entries with valid test data.
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
        }
    ];

    /// <summary>
    /// Property 5.1: For successful task results, optimized implementation produces identical results to LINQ.
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task GetPassesAsync_produces_identical_results_to_linq_implementation(
        byte satelliteMask, 
        byte passMask,
        NonNegativeInt minDurationMinutes)
    {
        // Generate test data based on input parameters
        var enabledSatellites = GenerateEnabledSatellites(satelliteMask);
        var passTestData = GeneratePassTestData(passMask, enabledSatellites);
        var minDuration = Math.Min(minDurationMinutes.Get, 60); // Cap at 60 minutes for reasonable test duration
        
        if (enabledSatellites.Count == 0)
            return; // Skip empty satellite collections
        
        var groundStation = new GroundStation 
        { 
            DisplayName = "Test Station",
            LatitudeDeg = 40.7589,
            LongitudeDeg = -73.9851,
            AltitudeMetersAsl = 100
        };

        // Create optimized orchestrator (current implementation)
        var optimizedOrchestrator = new TrackingOrchestrator(
            new StubSettingsService(),
            new StubTleService(enabledSatellites),
            new StubPropagator(),
            new StubGroundGeometry(),
            new ConfigurablePassPredictor(passTestData));

        // Create reference implementation orchestrator using LINQ
        var linqOrchestrator = new LinqReferenceTrackingOrchestrator(
            new StubSettingsService(),
            new StubTleService(enabledSatellites),
            new StubPropagator(),
            new StubGroundGeometry(),
            new ConfigurablePassPredictor(passTestData));

        // Execute both implementations
        var optimizedResults = await optimizedOrchestrator.GetPassesAsync(
            groundStation, 5.0, 24, minDuration);
        
        var linqResults = await linqOrchestrator.GetPassesAsync(
            groundStation, 5.0, 24, minDuration);

        // Verify identical results
        Assert.Equal(linqResults.Count, optimizedResults.Count);
        
        for (int i = 0; i < linqResults.Count; i++)
        {
            AssertPassInfoEqual(linqResults[i], optimizedResults[i]);
        }
    }

    /// <summary>
    /// Property 5.2: Edge cases produce identical results between implementations.
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task GetPassesAsync_handles_edge_cases_identically(EdgeCaseScenario scenario)
    {
        var groundStation = new GroundStation 
        { 
            DisplayName = "Test Station",
            LatitudeDeg = 40.7589,
            LongitudeDeg = -73.9851,
            AltitudeMetersAsl = 100
        };

        var (enabledSats, passData, minDuration) = GenerateEdgeCaseData(scenario);

        // Create both orchestrators
        var optimizedOrchestrator = new TrackingOrchestrator(
            new StubSettingsService(),
            new StubTleService(enabledSats),
            new StubPropagator(),
            new StubGroundGeometry(),
            new ConfigurablePassPredictor(passData));

        var linqOrchestrator = new LinqReferenceTrackingOrchestrator(
            new StubSettingsService(),
            new StubTleService(enabledSats),
            new StubPropagator(),
            new StubGroundGeometry(),
            new ConfigurablePassPredictor(passData));

        // Execute both implementations
        var optimizedResults = await optimizedOrchestrator.GetPassesAsync(
            groundStation, 5.0, 24, minDuration);
        
        var linqResults = await linqOrchestrator.GetPassesAsync(
            groundStation, 5.0, 24, minDuration);

        // Verify identical results
        Assert.Equal(linqResults.Count, optimizedResults.Count);
        
        for (int i = 0; i < linqResults.Count; i++)
        {
            AssertPassInfoEqual(linqResults[i], optimizedResults[i]);
        }
    }

    /// <summary>
    /// Property 5.3: Exception scenarios are handled identically.
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task GetPassesAsync_handles_exceptions_identically(byte failureMask)
    {
        var enabledSatellites = SatellitePool.Take(3).ToList();
        var passPredictorWithFailures = new FailingPassPredictor(failureMask);

        var groundStation = new GroundStation 
        { 
            DisplayName = "Test Station",
            LatitudeDeg = 40.7589,
            LongitudeDeg = -73.9851,
            AltitudeMetersAsl = 100
        };

        // Create both orchestrators
        var optimizedOrchestrator = new TrackingOrchestrator(
            new StubSettingsService(),
            new StubTleService(enabledSatellites),
            new StubPropagator(),
            new StubGroundGeometry(),
            passPredictorWithFailures);

        var linqOrchestrator = new LinqReferenceTrackingOrchestrator(
            new StubSettingsService(),
            new StubTleService(enabledSatellites),
            new StubPropagator(),
            new StubGroundGeometry(),
            passPredictorWithFailures);

        // Execute both implementations and verify they handle failures identically
        var optimizedResults = await optimizedOrchestrator.GetPassesAsync(
            groundStation, 5.0, 24, 0);
        
        var linqResults = await linqOrchestrator.GetPassesAsync(
            groundStation, 5.0, 24, 0);

        // Both should collect partial results when tasks fail
        Assert.Equal(linqResults.Count, optimizedResults.Count);
        
        for (int i = 0; i < linqResults.Count; i++)
        {
            AssertPassInfoEqual(linqResults[i], optimizedResults[i]);
        }
    }

    private static List<SatelliteCatalogEntry> GenerateEnabledSatellites(byte mask)
    {
        var satellites = new List<SatelliteCatalogEntry>();
        for (int i = 0; i < SatellitePool.Length; i++)
        {
            if ((mask & (1 << i)) != 0)
            {
                satellites.Add(SatellitePool[i]);
            }
        }
        return satellites;
    }

    private static Dictionary<string, List<PassInfo>> GeneratePassTestData(byte passMask, List<SatelliteCatalogEntry> satellites)
    {
        var data = new Dictionary<string, List<PassInfo>>();
        var baseTime = DateTime.UtcNow.AddHours(1);
        
        foreach (var satellite in satellites)
        {
            var passes = new List<PassInfo>();
            var satIndex = Array.IndexOf(SatellitePool, satellite);
            
            // Generate 0-3 passes based on mask
            var passCount = (passMask >> (satIndex * 2)) & 0x03;
            
            for (int i = 0; i < passCount; i++)
            {
                var aos = baseTime.AddHours(i * 2);
                var duration = TimeSpan.FromMinutes(5 + i * 5); // Varying durations
                
                passes.Add(new PassInfo
                {
                    SatelliteName = satellite.Name,
                    NoradId = satellite.NoradId,
                    AosUtc = aos,
                    LosUtc = aos.Add(duration),
                    MaxElevationDeg = 30.0 + i * 10.0,
                    MaxElevationUtc = aos.Add(duration.Divide(2)),
                    AosAzimuthDeg = 45.0 + i * 30.0,
                    LosAzimuthDeg = 135.0 + i * 30.0
                });
            }
            
            data[satellite.NoradId] = passes;
        }
        
        return data;
    }

    private static (List<SatelliteCatalogEntry>, Dictionary<string, List<PassInfo>>, int) GenerateEdgeCaseData(EdgeCaseScenario scenario)
    {
        return scenario switch
        {
            EdgeCaseScenario.EmptyResults => (SatellitePool.Take(1).ToList(), new Dictionary<string, List<PassInfo>>(), 0),
            EdgeCaseScenario.ZeroDurationFilter => (SatellitePool.Take(2).ToList(), GenerateZeroDurationPasses(), 0),
            EdgeCaseScenario.AllFilteredOut => (SatellitePool.Take(2).ToList(), GenerateShortPasses(), 60),
            EdgeCaseScenario.SinglePass => (SatellitePool.Take(1).ToList(), GenerateSinglePass(), 0),
            _ => ([], new Dictionary<string, List<PassInfo>>(), 0)
        };
    }

    private static Dictionary<string, List<PassInfo>> GenerateZeroDurationPasses()
    {
        var baseTime = DateTime.UtcNow;
        return new Dictionary<string, List<PassInfo>>
        {
            [SatellitePool[0].NoradId] = [new PassInfo
            {
                SatelliteName = SatellitePool[0].Name,
                NoradId = SatellitePool[0].NoradId,
                AosUtc = baseTime,
                LosUtc = baseTime, // Zero duration
                MaxElevationDeg = 45.0,
                MaxElevationUtc = baseTime,
                AosAzimuthDeg = 180.0,
                LosAzimuthDeg = 180.0
            }]
        };
    }

    private static Dictionary<string, List<PassInfo>> GenerateShortPasses()
    {
        var baseTime = DateTime.UtcNow;
        return new Dictionary<string, List<PassInfo>>
        {
            [SatellitePool[0].NoradId] = [new PassInfo
            {
                SatelliteName = SatellitePool[0].Name,
                NoradId = SatellitePool[0].NoradId,
                AosUtc = baseTime,
                LosUtc = baseTime.AddMinutes(1), // Very short duration that will be filtered out
                MaxElevationDeg = 45.0,
                MaxElevationUtc = baseTime.AddSeconds(30),
                AosAzimuthDeg = 180.0,
                LosAzimuthDeg = 200.0
            }]
        };
    }

    private static Dictionary<string, List<PassInfo>> GenerateSinglePass()
    {
        var baseTime = DateTime.UtcNow.AddHours(1);
        return new Dictionary<string, List<PassInfo>>
        {
            [SatellitePool[0].NoradId] = [new PassInfo
            {
                SatelliteName = SatellitePool[0].Name,
                NoradId = SatellitePool[0].NoradId,
                AosUtc = baseTime,
                LosUtc = baseTime.AddMinutes(10),
                MaxElevationDeg = 45.0,
                MaxElevationUtc = baseTime.AddMinutes(5),
                AosAzimuthDeg = 180.0,
                LosAzimuthDeg = 270.0
            }]
        };
    }

    private static void AssertPassInfoEqual(PassInfo expected, PassInfo actual)
    {
        Assert.Equal(expected.SatelliteName, actual.SatelliteName);
        Assert.Equal(expected.NoradId, actual.NoradId);
        Assert.Equal(expected.AosUtc, actual.AosUtc);
        Assert.Equal(expected.LosUtc, actual.LosUtc);
        Assert.Equal(expected.MaxElevationDeg, actual.MaxElevationDeg, 6);
        Assert.Equal(expected.MaxElevationUtc, actual.MaxElevationUtc);
        Assert.Equal(expected.AosAzimuthDeg, actual.AosAzimuthDeg, 6);
        Assert.Equal(expected.LosAzimuthDeg, actual.LosAzimuthDeg, 6);
    }

    public enum EdgeCaseScenario : byte
    {
        EmptyResults = 0,
        ZeroDurationFilter = 1,
        AllFilteredOut = 2,
        SinglePass = 3
    }

    #region Reference Implementation and Test Doubles

    /// <summary>
    /// Reference implementation that uses the original LINQ approach for comparison.
    /// This simulates what the GetPassesAsync method looked like before optimization.
    /// </summary>
    private sealed class LinqReferenceTrackingOrchestrator
    {
        private readonly ISettingsService _settings;
        private readonly ITleService _tleService;
        private readonly IOrbitPropagator _propagator;
        private readonly IGroundGeometry _groundGeometry;
        private readonly IPassPredictor _passPredictor;

        public LinqReferenceTrackingOrchestrator(
            ISettingsService settings,
            ITleService tleService,
            IOrbitPropagator propagator,
            IGroundGeometry groundGeometry,
            IPassPredictor passPredictor)
        {
            _settings = settings;
            _tleService = tleService;
            _propagator = propagator;
            _groundGeometry = groundGeometry;
            _passPredictor = passPredictor;
        }

        /// <summary>
        /// Original LINQ-based implementation for comparison with optimized version.
        /// </summary>
        public async Task<IReadOnlyList<PassInfo>> GetPassesAsync(
            GroundStation site,
            double minimumElevationDeg,
            int predictionHours,
            int minimumDurationMinutes,
            CancellationToken cancellationToken = default)
        {
            var utcStart = DateTime.UtcNow;
            var utcEnd = utcStart.AddHours(predictionHours);
            var minDuration = TimeSpan.FromMinutes(Math.Max(0, minimumDurationMinutes));

            var sats = _tleService.GetEnabledSatellites(_settings.Current);
            
            // Original approach: use LINQ Select().ToList()
            var tasks = sats.Select(sat =>
                _passPredictor.GetPassesAsync(sat, site, utcStart, utcEnd, minimumElevationDeg, cancellationToken))
                .ToList();

            try
            {
                await Task.WhenAll(tasks);
            }
            catch
            {
                // Allow partial results to be collected below.
            }

            // Original LINQ chain
            return tasks
                .Where(t => t.IsCompletedSuccessfully)     // IEnumerable allocation
                .SelectMany(t => t.Result)                 // IEnumerable + SelectMany buffer
                .Where(p => p.Duration >= minDuration)     // IEnumerable allocation  
                .OrderBy(p => p.AosUtc)                    // Array allocation for sorting
                .ToList();                                 // Final List allocation
        }
    }

    private sealed class StubSettingsService : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public string? LoadError => null;
        public bool CanPersist => true;
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

        public StubTleService(IReadOnlyList<SatelliteCatalogEntry> enabled) => _enabled = enabled;

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

    private sealed class StubPropagator : IOrbitPropagator
    {
        public IReadOnlyCollection<string> LoadedNoradIds => [];
        public void LoadSatellite(SatelliteCatalogEntry entry) { }
        public void RemoveSatellite(string noradId) { }
        public void Clear() { }
        public bool HasSatellite(string noradId) => false;
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

    private sealed class ConfigurablePassPredictor : IPassPredictor
    {
        private readonly Dictionary<string, List<PassInfo>> _passData;

        public ConfigurablePassPredictor(Dictionary<string, List<PassInfo>> passData)
        {
            _passData = passData;
        }

        public Task<IReadOnlyList<PassInfo>> GetPassesAsync(
            SatelliteCatalogEntry satellite,
            GroundStation site,
            DateTime utcStart,
            DateTime utcEnd,
            double minimumElevationDeg,
            CancellationToken cancellationToken = default)
        {
            if (_passData.TryGetValue(satellite.NoradId, out var passes))
            {
                return Task.FromResult<IReadOnlyList<PassInfo>>(passes);
            }
            
            return Task.FromResult<IReadOnlyList<PassInfo>>([]);
        }
    }

    private sealed class FailingPassPredictor : IPassPredictor
    {
        private readonly byte _failureMask;

        public FailingPassPredictor(byte failureMask)
        {
            _failureMask = failureMask;
        }

        public Task<IReadOnlyList<PassInfo>> GetPassesAsync(
            SatelliteCatalogEntry satellite,
            GroundStation site,
            DateTime utcStart,
            DateTime utcEnd,
            double minimumElevationDeg,
            CancellationToken cancellationToken = default)
        {
            var satIndex = Array.IndexOf(SatellitePool, satellite);
            if (satIndex >= 0 && (_failureMask & (1 << satIndex)) != 0)
            {
                return Task.FromException<IReadOnlyList<PassInfo>>(
                    new InvalidOperationException($"Prediction failed for {satellite.NoradId}"));
            }

            // Return empty result for successful satellites
            return Task.FromResult<IReadOnlyList<PassInfo>>([]);
        }
    }

    #endregion
}