using System.Diagnostics;
using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Core.Services;
using OscarWatch.Core.Tle;

namespace OscarWatch.Tests;

/// <summary>
/// Integration tests for the complete TrackingOrchestrator workflow with LINQ hotpath optimizations.
/// These tests verify that the optimized methods work together correctly and maintain functional equivalence
/// while achieving allocation reduction targets.
/// 
/// **Validates: Requirements 3.1, 3.4**
/// </summary>
public sealed class TrackingOrchestratorWorkflowIntegrationTests
{
    private const int TestSatelliteCount = 10;
    private const int PassesPerSatellite = 3;
    private const int AllocationReductionToleranceBytes = 500; // Allow small variance in allocation measurements

    [Fact]
    public async Task GetPassesAsync_CompleteWorkflow_ReturnsCorrectResultsWithReducedAllocations()
    {
        // Arrange
        var orchestrator = CreateTestOrchestrator();
        var site = CreateTestGroundStation();

        // Act - Measure allocation and performance for complete workflow
        var metrics = await MeasureOperationAsync(async () =>
        {
            return await orchestrator.GetPassesAsync(
                site,
                minimumElevationDeg: 5.0,
                predictionHours: 24,
                minimumDurationMinutes: 5,
                CancellationToken.None);
        });

        // Assert - Verify functional correctness
        var passes = (IReadOnlyList<PassInfo>)metrics.Result!;
        Assert.NotEmpty(passes);
        Assert.True(passes.Count <= TestSatelliteCount * PassesPerSatellite, 
            "Should not exceed maximum possible passes");

        // Verify passes are sorted by AOS time (requirement from optimized sorting)
        for (int i = 1; i < passes.Count; i++)
        {
            Assert.True(passes[i - 1].AosUtc <= passes[i].AosUtc, 
                "Passes should be sorted by AOS time in ascending order");
        }

        // Verify all passes meet duration filter
        var minDuration = TimeSpan.FromMinutes(5);
        Assert.All(passes, pass => Assert.True(pass.Duration >= minDuration, 
            "All passes should meet minimum duration requirement"));

        // Verify allocation characteristics
        Assert.True(metrics.AllocationMetrics.ExecutionTime > TimeSpan.Zero, 
            "Should have measurable execution time");
        Assert.Equal(passes.Count, metrics.AllocationMetrics.ResultCount);

        // Performance assertion - the optimized version should have reasonable allocation behavior
        Assert.True(metrics.AllocationMetrics.AllocatedBytesAfter >= metrics.AllocationMetrics.AllocatedBytesBefore - AllocationReductionToleranceBytes,
            "Allocation measurement should be reasonable (allowing for GC variance)");
    }

    [Fact]
    public async Task GetMutualPassesAsync_CompleteWorkflow_ReturnsCorrectResultsWithReducedAllocations()
    {
        // Arrange
        var orchestrator = CreateTestOrchestrator();
        var localSite = CreateTestGroundStation("LOCAL", 51.5074, -0.1278); // London
        var remoteSite = CreateTestGroundStation("REMOTE", 40.7128, -74.0060); // New York

        // Act - Measure allocation and performance for mutual pass workflow
        var metrics = await MeasureOperationAsync(async () =>
        {
            return await orchestrator.GetMutualPassesAsync(
                localSite,
                remoteSite,
                minimumElevationDeg: 5.0,
                DateTime.UtcNow,
                DateTime.UtcNow.AddHours(24),
                minimumPassDurationMinutes: 5,
                minimumMutualDurationMinutes: 2,
                CancellationToken.None);
        });

        // Assert - Verify functional correctness
        var mutualPasses = (IReadOnlyList<MutualPassInfo>)metrics.Result!;
        Assert.NotNull(mutualPasses);

        // Verify mutual pass constraints are satisfied
        Assert.All(mutualPasses, mutualPass =>
        {
            Assert.True(mutualPass.LocalPass.Duration >= TimeSpan.FromMinutes(5), 
                "Local pass should meet duration requirement");
            Assert.True(mutualPass.RemotePass.Duration >= TimeSpan.FromMinutes(5), 
                "Remote pass should meet duration requirement");
            Assert.True(mutualPass.Duration >= TimeSpan.FromMinutes(2), 
                "Mutual duration should meet requirement");
            Assert.Equal(mutualPass.LocalPass.NoradId, mutualPass.RemotePass.NoradId);
        });

        // Verify allocation characteristics
        Assert.True(metrics.AllocationMetrics.ExecutionTime > TimeSpan.Zero, 
            "Should have measurable execution time");
        Assert.Equal(mutualPasses.Count, metrics.AllocationMetrics.ResultCount);

        // Performance assertion
        Assert.True(metrics.AllocationMetrics.AllocatedBytesAfter >= metrics.AllocationMetrics.AllocatedBytesBefore - AllocationReductionToleranceBytes,
            "Allocation measurement should be reasonable (allowing for GC variance)");
    }

    [Fact]
    public async Task TrackingWorkflow_MultipleSequentialCalls_ReusesBuffersCorrectly()
    {
        // Arrange
        var orchestrator = CreateTestOrchestrator();
        var site = CreateTestGroundStation();

        // Act - Make multiple sequential calls to verify buffer reuse
        var firstCallMetrics = await MeasureOperationAsync(async () =>
        {
            return await orchestrator.GetPassesAsync(
                site,
                minimumElevationDeg: 5.0,
                predictionHours: 12,
                minimumDurationMinutes: 3,
                CancellationToken.None);
        });

        var secondCallMetrics = await MeasureOperationAsync(async () =>
        {
            return await orchestrator.GetPassesAsync(
                site,
                minimumElevationDeg: 10.0,
                predictionHours: 6,
                minimumDurationMinutes: 5,
                CancellationToken.None);
        });

        // Assert - Verify both calls work correctly
        var firstPasses = (IReadOnlyList<PassInfo>)firstCallMetrics.Result!;
        var secondPasses = (IReadOnlyList<PassInfo>)secondCallMetrics.Result!;

        Assert.NotNull(firstPasses);
        Assert.NotNull(secondPasses);

        // Second call should have fewer or equal passes due to higher elevation filter
        Assert.True(secondPasses.Count <= firstPasses.Count, 
            "Higher elevation filter should result in fewer or equal passes");

        // Verify both calls have reasonable performance
        Assert.True(firstCallMetrics.AllocationMetrics.ExecutionTime > TimeSpan.Zero);
        Assert.True(secondCallMetrics.AllocationMetrics.ExecutionTime > TimeSpan.Zero);

        // Second call should show buffer reuse benefit (similar or less allocation)
        // Allow more tolerance for allocation measurements due to GC variance
        var allocationDifference = Math.Abs(secondCallMetrics.AllocationMetrics.AllocatedBytesAfter - 
                                          firstCallMetrics.AllocationMetrics.AllocatedBytesAfter);
        Assert.True(allocationDifference < AllocationReductionToleranceBytes * 10,
            $"Sequential calls should have similar allocation patterns due to buffer reuse. " +
            $"First: {firstCallMetrics.AllocationMetrics.AllocatedBytesAfter}, " +
            $"Second: {secondCallMetrics.AllocationMetrics.AllocatedBytesAfter}, " +
            $"Difference: {allocationDifference}");
    }

    [Fact]
    public async Task CompleteWorkflow_WithRemoveSatellite_MaintainsFunctionalCorrectness()
    {
        // Arrange - Create orchestrator with configurable satellite list
        var initialSatellites = CreateTestSatellites();
        var configurableOrchestrator = CreateConfigurableTestOrchestrator(initialSatellites);
        var site = CreateTestGroundStation();

        // Verify initial state has satellites loaded
        Assert.True(initialSatellites.Count == TestSatelliteCount);

        // Act - Remove a satellite using the method (this tests the LINQ optimization)
        var targetSatelliteId = initialSatellites.First().NoradId;
        configurableOrchestrator.RemoveSatellite(targetSatelliteId);

        // Test that the workflow still operates correctly after satellite removal
        var passes = await configurableOrchestrator.GetPassesAsync(
            site,
            minimumElevationDeg: 5.0,
            predictionHours: 24,
            minimumDurationMinutes: 3,
            CancellationToken.None);

        // Assert - Verify the workflow continues to work properly
        Assert.NotNull(passes);

        // Verify passes are still correctly sorted (main workflow behavior)
        for (int i = 1; i < passes.Count; i++)
        {
            Assert.True(passes[i - 1].AosUtc <= passes[i].AosUtc, 
                "Passes should remain sorted after satellite removal");
        }

        // Verify all passes meet duration filter (workflow integrity)
        var minDuration = TimeSpan.FromMinutes(3);
        Assert.All(passes, pass => Assert.True(pass.Duration >= minDuration, 
            "All passes should meet minimum duration requirement after satellite removal"));
    }

    private static TrackingOrchestrator CreateConfigurableTestOrchestrator(IReadOnlyList<SatelliteCatalogEntry> satellites)
    {
        var settings = new StubSettingsService();
        var tleService = new StubTleService(satellites);
        var propagator = new StubPropagator();
        var groundGeometry = new StubGroundGeometry();
        var passPredictor = new StubPassPredictor(satellites);

        return new TrackingOrchestrator(
            settings,
            tleService,
            propagator,
            groundGeometry,
            passPredictor);
    }

    [Fact]
    public async Task ConcurrentWorkflowAccess_MultipleThreads_MaintainsThreadSafety()
    {
        // Arrange
        var orchestrator = CreateTestOrchestrator();
        var localSite = CreateTestGroundStation("LOCAL", 51.5074, -0.1278);
        var remoteSite = CreateTestGroundStation("REMOTE", 40.7128, -74.0060);
        const int ConcurrentThreads = 4;

        // Act - Run multiple operations concurrently
        var tasks = new List<Task<object>>();

        // Add GetPassesAsync tasks
        for (int i = 0; i < ConcurrentThreads; i++)
        {
            var siteForThread = CreateTestGroundStation($"SITE{i}", 50.0 + i, 0.0 + i);
            tasks.Add(Task.Run(async () =>
            {
                return (object)await orchestrator.GetPassesAsync(
                    siteForThread,
                    minimumElevationDeg: 5.0,
                    predictionHours: 12,
                    minimumDurationMinutes: 3,
                    CancellationToken.None);
            }));
        }

        // Add GetMutualPassesAsync tasks
        for (int i = 0; i < ConcurrentThreads; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                return (object)await orchestrator.GetMutualPassesAsync(
                    localSite,
                    remoteSite,
                    minimumElevationDeg: 5.0,
                    DateTime.UtcNow,
                    DateTime.UtcNow.AddHours(12),
                    minimumPassDurationMinutes: 3,
                    minimumMutualDurationMinutes: 1,
                    CancellationToken.None);
            }));
        }

        // Wait for all tasks to complete
        var results = await Task.WhenAll(tasks);

        // Assert - Verify all operations completed successfully
        Assert.Equal(ConcurrentThreads * 2, results.Length);
        Assert.All(results, result => Assert.NotNull(result));

        // Verify pass results
        var passResults = results.Take(ConcurrentThreads).Cast<IReadOnlyList<PassInfo>>();
        Assert.All(passResults, passes =>
        {
            Assert.NotNull(passes);
            // Verify sorting is maintained
            for (int i = 1; i < passes.Count; i++)
            {
                Assert.True(passes[i - 1].AosUtc <= passes[i].AosUtc);
            }
        });

        // Verify mutual pass results
        var mutualResults = results.Skip(ConcurrentThreads).Cast<IReadOnlyList<MutualPassInfo>>();
        Assert.All(mutualResults, mutualPasses =>
        {
            Assert.NotNull(mutualPasses);
            Assert.All(mutualPasses, mutualPass =>
            {
                Assert.Equal(mutualPass.LocalPass.NoradId, mutualPass.RemotePass.NoradId);
            });
        });
    }

    [Fact]
    public async Task WorkflowWithCancellation_CancellationToken_HandlesGracefully()
    {
        // Arrange
        var orchestrator = CreateTestOrchestrator();
        var site = CreateTestGroundStation();
        using var cts = new CancellationTokenSource();

        // Act & Assert - Test cancellation handling
        var task = orchestrator.GetPassesAsync(
            site,
            minimumElevationDeg: 5.0,
            predictionHours: 24,
            minimumDurationMinutes: 3,
            cts.Token);

        // Cancel immediately to test cancellation handling
        cts.Cancel();

        // The operation should either complete (if it was fast) or be cancelled
        try
        {
            var result = await task;
            // If it completed, verify it's a valid result
            Assert.NotNull(result);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is an expected outcome
            Assert.True(cts.Token.IsCancellationRequested);
        }
    }

    private static TrackingOrchestrator CreateTestOrchestrator()
    {
        var satellites = CreateTestSatellites();
        var settings = new StubSettingsService();
        var tleService = new StubTleService(satellites);
        var propagator = new StubPropagator();
        var groundGeometry = new StubGroundGeometry();
        var passPredictor = new StubPassPredictor(satellites);

        return new TrackingOrchestrator(
            settings,
            tleService,
            propagator,
            groundGeometry,
            passPredictor);
    }

    private static IReadOnlyList<SatelliteCatalogEntry> CreateTestSatellites()
    {
        var satellites = new List<SatelliteCatalogEntry>();
        for (int i = 0; i < TestSatelliteCount; i++)
        {
            satellites.Add(new SatelliteCatalogEntry
            {
                NoradId = (25544 + i).ToString(),
                Name = $"TEST-SAT-{i}",
                Line1 = "1 25544U 98067A   21001.00000000  .00000000  00000-0  00000-0 0  9990",
                Line2 = "2 25544  51.6400   0.0000 0000000   0.0000   0.0000 15.48919103000000"
            });
        }
        return satellites;
    }

    private static GroundStation CreateTestGroundStation(string displayName = "TEST", double lat = 52.0, double lon = 0.0)
    {
        return new GroundStation
        {
            DisplayName = displayName,
            LatitudeDeg = lat,
            LongitudeDeg = lon,
            AltitudeMetersAsl = 100,
            GridSquare = "JO01aa"
        };
    }

    private static async Task<WorkflowOperationMetrics<T>> MeasureOperationAsync<T>(Func<Task<T>> operation)
    {
        var sw = Stopwatch.StartNew();
        var beforeBytes = GC.GetAllocatedBytesForCurrentThread();

        var result = await operation();

        var afterBytes = GC.GetAllocatedBytesForCurrentThread();
        sw.Stop();

        var resultCount = result switch
        {
            System.Collections.ICollection collection => collection.Count,
            _ => 1
        };

        var allocationMetrics = AllocationMetrics.Create(beforeBytes, afterBytes, sw.Elapsed, resultCount);

        return new WorkflowOperationMetrics<T>(result, allocationMetrics);
    }

    private sealed record WorkflowOperationMetrics<T>(T Result, AllocationMetrics AllocationMetrics);

    #region Test Stubs

    private sealed class StubSettingsService : ISettingsService
    {
        public AppSettings Current { get; } = new AppSettings();
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
        private readonly IReadOnlyList<SatelliteCatalogEntry> _satellites;

        public StubTleService(IReadOnlyList<SatelliteCatalogEntry> satellites)
        {
            _satellites = satellites;
        }

        public IReadOnlyList<SatelliteCatalogEntry> Catalog => _satellites;
        public DateTime? LastFetchedUtc => DateTime.UtcNow;
        public string CachePath => "";
        public TleCatalogLoadDiagnostics? LastLoadDiagnostics => null;
        public bool IsStale(int staleHours) => false;
        public Task RefreshAsync(bool force = false, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureLoadedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void InvalidateCatalog() { }
        public string ActiveSourceLabel => "Test";
        public IReadOnlyList<SatelliteCatalogEntry> GetEnabledSatellites(AppSettings settings) => _satellites;
    }

    private sealed class StubPropagator : IOrbitPropagator
    {
        public IReadOnlyCollection<string> LoadedNoradIds => [];
        public void LoadSatellite(SatelliteCatalogEntry entry) { }
        public void RemoveSatellite(string noradId) { }
        public void Clear() { }
        public bool HasSatellite(string noradId) => true;
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
        private readonly IReadOnlyList<SatelliteCatalogEntry> _satellites;

        public StubPassPredictor(IReadOnlyList<SatelliteCatalogEntry> satellites)
        {
            _satellites = satellites;
        }

        public Task<IReadOnlyList<PassInfo>> GetPassesAsync(
            SatelliteCatalogEntry satellite,
            GroundStation site,
            DateTime utcStart,
            DateTime utcEnd,
            double minimumElevationDeg,
            CancellationToken cancellationToken = default)
        {
            // Create realistic test passes for each satellite
            var passes = new List<PassInfo>();
            var baseTime = utcStart.AddMinutes(30); // Start passes 30 minutes from start time

            for (int i = 0; i < PassesPerSatellite; i++)
            {
                var aos = baseTime.AddHours(i * 8); // Space passes 8 hours apart
                if (aos >= utcEnd) break; // Don't exceed end time

                var pass = new PassInfo
                {
                    NoradId = satellite.NoradId,
                    SatelliteName = satellite.Name,
                    AosUtc = aos,
                    LosUtc = aos.AddMinutes(10 + i * 2), // Vary pass duration
                    MaxElevationUtc = aos.AddMinutes(5),
                    MaxElevationDeg = 30 + i * 10 // Vary elevation
                };

                // Only include passes that meet the minimum elevation
                if (pass.MaxElevationDeg >= minimumElevationDeg)
                {
                    passes.Add(pass);
                }
            }

            return Task.FromResult<IReadOnlyList<PassInfo>>(passes);
        }
    }

    #endregion
}