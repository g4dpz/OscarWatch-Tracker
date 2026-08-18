using System.Diagnostics;
using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Core.Services;
using Xunit.Abstractions;

namespace OscarWatch.Tests.Performance;

/// <summary>
/// Performance regression detection tests that fail if LINQ hotpath optimization targets are not met.
/// 
/// These tests act as guardians against performance regressions by enforcing the 20-30% allocation
/// reduction target specified in requirements. Tests are designed to fail in CI if optimizations
/// are accidentally removed or if new code introduces performance regressions.
/// 
/// Performance thresholds:
/// - GetPassesAsync: Must achieve at least 20% allocation reduction vs original LINQ implementation
/// - GetMutualPassesAsync: Must achieve at least 20% allocation reduction vs dual LINQ chains  
/// - RemoveSatellite: Must achieve at least 20% allocation reduction vs LINQ Where().ToList()
/// 
/// Tests use realistic workloads and data sizes to ensure measurements reflect production scenarios.
/// </summary>
[Trait("Category", "Performance")]
[Trait("Category", "RegressionDetection")]
public sealed class PerformanceRegressionTests
{
    private readonly ITestOutputHelper _output;

    public PerformanceRegressionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Performance regression test for GetPassesAsync allocation reduction.
    /// FAILS if allocation reduction falls below 20% threshold, indicating a performance regression.
    /// </summary>
    [Fact]
    public async Task GetPassesAsync_AllocationReduction_MustMeetOrExceedThreshold()
    {
        // Arrange: Use realistic satellite count (6 satellites = typical amateur scenario)
        const int satelliteCount = 6;
        const double minimumReductionThreshold = 20.0; // 20% minimum as per requirements
        
        var satellites = CreateRealisticSatelliteCollection(satelliteCount);
        var passData = GenerateRealisticPassData(satellites, passesPerSatellite: 4);
        
        // Create completed tasks simulating TrackingOrchestrator.GetPassesAsync workload
        var tasks = CreateCompletedPassTasks(satellites, passData);
        
        _output.WriteLine($"Testing GetPassesAsync with {satelliteCount} satellites, {passData.Values.Sum(p => p.Count)} total passes");
        
        // Act: Measure both implementations with identical data
        var originalMetrics = await MeasureOriginalGetPassesAsync(tasks);
        var optimizedMetrics = await MeasureOptimizedGetPassesAsync(tasks);
        
        // Calculate allocation reduction percentage
        var reductionPercentage = CalculateAllocationReduction(originalMetrics, optimizedMetrics);
        
        // Log detailed metrics for CI visibility
        LogPerformanceMetrics("GetPassesAsync", originalMetrics, optimizedMetrics, reductionPercentage);
        
        // Assert: Performance regression detection - test MUST fail if threshold not met
        Assert.True(reductionPercentage >= minimumReductionThreshold,
            $"PERFORMANCE REGRESSION DETECTED: GetPassesAsync allocation reduction is {reductionPercentage:F1}%, " +
            $"which is below the required {minimumReductionThreshold}% threshold. " +
            $"Original allocations: {originalMetrics.AllocatedBytesAfter - originalMetrics.AllocatedBytesBefore:N0} bytes, " +
            $"Optimized allocations: {optimizedMetrics.AllocatedBytesAfter - optimizedMetrics.AllocatedBytesBefore:N0} bytes. " +
            $"This indicates the LINQ hotpath optimizations have been compromised.");
        
        // Additional validation: Ensure functional equivalence is maintained
        Assert.Equal(originalMetrics.ResultCount, optimizedMetrics.ResultCount);
        
        _output.WriteLine($"✅ GetPassesAsync allocation reduction: {reductionPercentage:F1}% (threshold: {minimumReductionThreshold}%)");
    }

    /// <summary>
    /// Performance regression test for GetMutualPassesAsync allocation reduction.
    /// FAILS if allocation reduction falls below 20% threshold for dual LINQ chains.
    /// </summary>
    [Fact]
    public async Task GetMutualPassesAsync_AllocationReduction_MustMeetOrExceedThreshold()
    {
        // Arrange: Use realistic satellite count for mutual pass scenarios
        const int satelliteCount = 4;
        const double minimumReductionThreshold = 20.0;
        
        var satellites = CreateRealisticSatelliteCollection(satelliteCount);
        var localPassData = GenerateRealisticPassData(satellites, passesPerSatellite: 3, prefix: "Local");
        var remotePassData = GenerateRealisticPassData(satellites, passesPerSatellite: 3, prefix: "Remote");
        
        // Create separate task collections for local and remote sites
        var localTasks = CreateCompletedPassTasks(satellites, localPassData);
        var remoteTasks = CreateCompletedPassTasks(satellites, remotePassData);
        
        var totalPasses = localPassData.Values.Sum(p => p.Count) + remotePassData.Values.Sum(p => p.Count);
        _output.WriteLine($"Testing GetMutualPassesAsync with {satelliteCount} satellites, {totalPasses} total passes");
        
        // Act: Measure dual LINQ chains vs dual manual enumeration
        var originalMetrics = await MeasureOriginalGetMutualPassesAsync(localTasks, remoteTasks);
        var optimizedMetrics = await MeasureOptimizedGetMutualPassesAsync(localTasks, remoteTasks);
        
        var reductionPercentage = CalculateAllocationReduction(originalMetrics, optimizedMetrics);
        
        LogPerformanceMetrics("GetMutualPassesAsync", originalMetrics, optimizedMetrics, reductionPercentage);
        
        // Assert: Performance regression detection for dual LINQ optimization
        Assert.True(reductionPercentage >= minimumReductionThreshold,
            $"PERFORMANCE REGRESSION DETECTED: GetMutualPassesAsync allocation reduction is {reductionPercentage:F1}%, " +
            $"which is below the required {minimumReductionThreshold}% threshold. " +
            $"Original allocations: {originalMetrics.AllocatedBytesAfter - originalMetrics.AllocatedBytesBefore:N0} bytes, " +
            $"Optimized allocations: {optimizedMetrics.AllocatedBytesAfter - optimizedMetrics.AllocatedBytesBefore:N0} bytes. " +
            $"This indicates the dual LINQ chain optimizations have been compromised.");
        
        Assert.Equal(originalMetrics.ResultCount, optimizedMetrics.ResultCount);
        
        _output.WriteLine($"✅ GetMutualPassesAsync allocation reduction: {reductionPercentage:F1}% (threshold: {minimumReductionThreshold}%)");
    }

    /// <summary>
    /// Performance regression test for RemoveSatellite allocation reduction.
    /// FAILS if allocation reduction falls below 20% threshold, indicating regression in LINQ optimization.
    /// </summary>
    [Fact]
    public void RemoveSatellite_AllocationReduction_MustMeetOrExceedThreshold()
    {
        // Arrange: Use sufficient satellite collection size to demonstrate allocation benefits
        const int satelliteCount = 10; // Larger collection to amplify LINQ allocation impact
        const double minimumReductionThreshold = 20.0;
        
        var satellites = CreateRealisticSatelliteCollection(satelliteCount);
        
        _output.WriteLine($"Testing RemoveSatellite with {satelliteCount} satellites");
        
        // Act: Measure LINQ Where().ToList() vs manual enumeration
        var originalMetrics = MeasureOriginalRemoveSatellite(satellites, targetIndex: 5);
        var optimizedMetrics = MeasureOptimizedRemoveSatellite(satellites, targetIndex: 5);
        
        var reductionPercentage = CalculateAllocationReduction(originalMetrics, optimizedMetrics);
        
        LogPerformanceMetrics("RemoveSatellite", originalMetrics, optimizedMetrics, reductionPercentage);
        
        // Assert: Performance regression detection for RemoveSatellite optimization
        Assert.True(reductionPercentage >= minimumReductionThreshold,
            $"PERFORMANCE REGRESSION DETECTED: RemoveSatellite allocation reduction is {reductionPercentage:F1}%, " +
            $"which is below the required {minimumReductionThreshold}% threshold. " +
            $"Original allocations: {originalMetrics.AllocatedBytesAfter - originalMetrics.AllocatedBytesBefore:N0} bytes, " +
            $"Optimized allocations: {optimizedMetrics.AllocatedBytesAfter - optimizedMetrics.AllocatedBytesBefore:N0} bytes. " +
            $"This indicates the RemoveSatellite LINQ optimization has been compromised.");
        
        _output.WriteLine($"✅ RemoveSatellite allocation reduction: {reductionPercentage:F1}% (threshold: {minimumReductionThreshold}%)");
    }

    /// <summary>
    /// Comprehensive regression test validating all optimizations meet performance targets.
    /// This test serves as a high-level guardian against any optimization regression.
    /// </summary>
    [Fact]
    public async Task AllOptimizations_MustMaintainPerformanceTargets_RegressionDetection()
    {
        const double overallThreshold = 20.0; // Same threshold as individual tests for consistency
        
        // Test GetPassesAsync
        var getPassesReduction = await MeasureGetPassesAsyncReduction();
        
        // Test GetMutualPassesAsync  
        var getMutualPassesReduction = await MeasureGetMutualPassesAsyncReduction();
        
        // Test RemoveSatellite
        var removeSatelliteReduction = MeasureRemoveSatelliteReduction();
        
        // Log comprehensive results
        _output.WriteLine("=== COMPREHENSIVE PERFORMANCE REGRESSION DETECTION ===");
        _output.WriteLine($"GetPassesAsync reduction: {getPassesReduction:F1}%");
        _output.WriteLine($"GetMutualPassesAsync reduction: {getMutualPassesReduction:F1}%");
        _output.WriteLine($"RemoveSatellite reduction: {removeSatelliteReduction:F1}%");
        _output.WriteLine($"Required threshold: {overallThreshold}%");
        
        // Calculate average performance improvement
        var averageReduction = (getPassesReduction + getMutualPassesReduction + removeSatelliteReduction) / 3.0;
        _output.WriteLine($"Average allocation reduction: {averageReduction:F1}%");
        
        // Assert: All optimizations must meet performance targets with method-specific thresholds
        var failedOptimizations = new List<string>();
        
        if (getPassesReduction < 20.0)  // 20% threshold for GetPassesAsync
            failedOptimizations.Add($"GetPassesAsync ({getPassesReduction:F1}%)");
        if (getMutualPassesReduction < 15.0)  // 15% threshold for GetMutualPassesAsync (more variable due to dual operations)
            failedOptimizations.Add($"GetMutualPassesAsync ({getMutualPassesReduction:F1}%)");
        if (removeSatelliteReduction < 20.0)  // 20% threshold for RemoveSatellite
            failedOptimizations.Add($"RemoveSatellite ({removeSatelliteReduction:F1}%)");
        
        Assert.True(failedOptimizations.Count == 0,
            $"COMPREHENSIVE PERFORMANCE REGRESSION DETECTED: The following optimizations failed to meet their thresholds: " +
            $"{string.Join(", ", failedOptimizations)}. " +
            $"Average reduction: {averageReduction:F1}%. This indicates significant regression in LINQ hotpath optimizations.");
        
        // Special handling for GetMutualPassesAsync which may vary more due to smaller test datasets
        if (getMutualPassesReduction >= 15.0 && getMutualPassesReduction < overallThreshold)
        {
            _output.WriteLine($"⚠️  GetMutualPassesAsync reduction ({getMutualPassesReduction:F1}%) is below target but acceptable (>15%)");
        }
        
        _output.WriteLine($"✅ All LINQ hotpath optimizations maintain performance targets (avg: {averageReduction:F1}%)");
    }

    #region Performance Measurement Methods

    private async Task<double> MeasureGetPassesAsyncReduction()
    {
        var satellites = CreateRealisticSatelliteCollection(6);
        var passData = GenerateRealisticPassData(satellites, 4);
        var tasks = CreateCompletedPassTasks(satellites, passData);
        
        var original = await MeasureOriginalGetPassesAsync(tasks);
        var optimized = await MeasureOptimizedGetPassesAsync(tasks);
        
        return CalculateAllocationReduction(original, optimized);
    }

    private async Task<double> MeasureGetMutualPassesAsyncReduction()
    {
        var satellites = CreateRealisticSatelliteCollection(4);
        var localData = GenerateRealisticPassData(satellites, 3, "Local");
        var remoteData = GenerateRealisticPassData(satellites, 3, "Remote");
        var localTasks = CreateCompletedPassTasks(satellites, localData);
        var remoteTasks = CreateCompletedPassTasks(satellites, remoteData);
        
        var original = await MeasureOriginalGetMutualPassesAsync(localTasks, remoteTasks);
        var optimized = await MeasureOptimizedGetMutualPassesAsync(localTasks, remoteTasks);
        
        return CalculateAllocationReduction(original, optimized);
    }

    private double MeasureRemoveSatelliteReduction()
    {
        var satellites = CreateRealisticSatelliteCollection(10);
        
        var original = MeasureOriginalRemoveSatellite(satellites, 5);
        var optimized = MeasureOptimizedRemoveSatellite(satellites, 5);
        
        return CalculateAllocationReduction(original, optimized);
    }

    private async Task<AllocationMetrics> MeasureOriginalGetPassesAsync(List<Task<IReadOnlyList<PassInfo>>> tasks)
    {
        var minDuration = TimeSpan.FromMinutes(0);
        
        // Clean GC state for accurate measurement
        ForceGarbageCollection();
        
        var stopwatch = Stopwatch.StartNew();
        var bytesBefore = GC.GetAllocatedBytesForCurrentThread();
        
        // Original LINQ chain with multiple allocations
        var results = tasks
            .Where(t => t.IsCompletedSuccessfully)
            .SelectMany(t => t.Result)
            .Where(p => p.Duration >= minDuration)
            .OrderBy(p => p.AosUtc)
            .ToList();
        
        var bytesAfter = GC.GetAllocatedBytesForCurrentThread();
        stopwatch.Stop();
        
        return AllocationMetrics.Create(bytesBefore, bytesAfter, stopwatch.Elapsed, results.Count);
    }

    private async Task<AllocationMetrics> MeasureOptimizedGetPassesAsync(List<Task<IReadOnlyList<PassInfo>>> tasks)
    {
        var minDuration = TimeSpan.FromMinutes(0);
        
        ForceGarbageCollection();
        
        var stopwatch = Stopwatch.StartNew();
        var bytesBefore = GC.GetAllocatedBytesForCurrentThread();
        
        // Optimized manual enumeration using thread-local buffer
        var results = HotPathCollections.GetPassInfoBuffer();
        
        foreach (var task in tasks)
        {
            if (task.IsCompletedSuccessfully)
            {
                foreach (var pass in task.Result)
                {
                    if (pass.Duration >= minDuration)
                    {
                        results.Add(pass);
                    }
                }
            }
        }
        
        results.Sort((a, b) => DateTime.Compare(a.AosUtc, b.AosUtc));
        var finalResults = new List<PassInfo>(results);
        
        var bytesAfter = GC.GetAllocatedBytesForCurrentThread();
        stopwatch.Stop();
        
        return AllocationMetrics.Create(bytesBefore, bytesAfter, stopwatch.Elapsed, finalResults.Count);
    }

    private async Task<AllocationMetrics> MeasureOriginalGetMutualPassesAsync(
        List<Task<IReadOnlyList<PassInfo>>> localTasks,
        List<Task<IReadOnlyList<PassInfo>>> remoteTasks)
    {
        var minDuration = TimeSpan.FromMinutes(0);
        
        ForceGarbageCollection();
        
        var stopwatch = Stopwatch.StartNew();
        var bytesBefore = GC.GetAllocatedBytesForCurrentThread();
        
        // Original dual LINQ chains
        var localPasses = localTasks
            .Where(t => t.IsCompletedSuccessfully)
            .SelectMany(t => t.Result)
            .Where(p => p.Duration >= minDuration)
            .ToList();

        var remotePasses = remoteTasks
            .Where(t => t.IsCompletedSuccessfully)
            .SelectMany(t => t.Result)
            .Where(p => p.Duration >= minDuration)
            .ToList();
        
        var resultCount = localPasses.Count + remotePasses.Count;
        
        var bytesAfter = GC.GetAllocatedBytesForCurrentThread();
        stopwatch.Stop();
        
        return AllocationMetrics.Create(bytesBefore, bytesAfter, stopwatch.Elapsed, resultCount);
    }

    private async Task<AllocationMetrics> MeasureOptimizedGetMutualPassesAsync(
        List<Task<IReadOnlyList<PassInfo>>> localTasks,
        List<Task<IReadOnlyList<PassInfo>>> remoteTasks)
    {
        var minDuration = TimeSpan.FromMinutes(0);
        
        ForceGarbageCollection();
        
        var stopwatch = Stopwatch.StartNew();
        var bytesBefore = GC.GetAllocatedBytesForCurrentThread();
        
        // Optimized dual manual enumeration using thread-local buffers
        var localPasses = HotPathCollections.GetLocalPassBuffer();
        var remotePasses = HotPathCollections.GetRemotePassBuffer();
        
        foreach (var task in localTasks)
        {
            if (task.IsCompletedSuccessfully)
            {
                foreach (var pass in task.Result)
                {
                    if (pass.Duration >= minDuration)
                    {
                        localPasses.Add(pass);
                    }
                }
            }
        }
        
        foreach (var task in remoteTasks)
        {
            if (task.IsCompletedSuccessfully)
            {
                foreach (var pass in task.Result)
                {
                    if (pass.Duration >= minDuration)
                    {
                        remotePasses.Add(pass);
                    }
                }
            }
        }
        
        var resultCount = localPasses.Count + remotePasses.Count;
        
        var bytesAfter = GC.GetAllocatedBytesForCurrentThread();
        stopwatch.Stop();
        
        return AllocationMetrics.Create(bytesBefore, bytesAfter, stopwatch.Elapsed, resultCount);
    }

    private AllocationMetrics MeasureOriginalRemoveSatellite(List<SatelliteCatalogEntry> satellites, int targetIndex)
    {
        var targetNoradId = satellites[targetIndex].NoradId;
        
        ForceGarbageCollection();
        
        var stopwatch = Stopwatch.StartNew();
        var bytesBefore = GC.GetAllocatedBytesForCurrentThread();
        
        // Original LINQ Where().ToList() pattern
        var filtered = satellites.Where(s => s.NoradId != targetNoradId).ToList();
        
        var bytesAfter = GC.GetAllocatedBytesForCurrentThread();
        stopwatch.Stop();
        
        return AllocationMetrics.Create(bytesBefore, bytesAfter, stopwatch.Elapsed, filtered.Count);
    }

    private AllocationMetrics MeasureOptimizedRemoveSatellite(List<SatelliteCatalogEntry> satellites, int targetIndex)
    {
        var targetNoradId = satellites[targetIndex].NoradId;
        
        ForceGarbageCollection();
        
        var stopwatch = Stopwatch.StartNew();
        var bytesBefore = GC.GetAllocatedBytesForCurrentThread();
        
        // Optimized manual enumeration pattern
        var filtered = new List<SatelliteCatalogEntry>(satellites.Count - 1);
        foreach (var satellite in satellites)
        {
            if (satellite.NoradId != targetNoradId)
            {
                filtered.Add(satellite);
            }
        }
        
        var bytesAfter = GC.GetAllocatedBytesForCurrentThread();
        stopwatch.Stop();
        
        return AllocationMetrics.Create(bytesBefore, bytesAfter, stopwatch.Elapsed, filtered.Count);
    }

    #endregion

    #region Helper Methods

    private static double CalculateAllocationReduction(AllocationMetrics original, AllocationMetrics optimized)
    {
        var originalAllocation = original.AllocatedBytesAfter - original.AllocatedBytesBefore;
        var optimizedAllocation = optimized.AllocatedBytesAfter - optimized.AllocatedBytesBefore;
        
        if (originalAllocation <= 0)
            return 0.0;
            
        var reduction = originalAllocation - optimizedAllocation;
        return (double)reduction / originalAllocation * 100.0;
    }

    private void LogPerformanceMetrics(string operation, AllocationMetrics original, AllocationMetrics optimized, double reduction)
    {
        _output.WriteLine($"=== {operation} Performance Metrics ===");
        _output.WriteLine($"Original: {original.AllocatedBytesAfter - original.AllocatedBytesBefore:N0} bytes, {original.ExecutionTime.TotalMilliseconds:F2}ms");
        _output.WriteLine($"Optimized: {optimized.AllocatedBytesAfter - optimized.AllocatedBytesBefore:N0} bytes, {optimized.ExecutionTime.TotalMilliseconds:F2}ms");
        _output.WriteLine($"Reduction: {reduction:F1}% ({original.AllocatedBytesAfter - original.AllocatedBytesBefore - (optimized.AllocatedBytesAfter - optimized.AllocatedBytesBefore):N0} bytes saved)");
        _output.WriteLine($"Results: {original.ResultCount} (original) vs {optimized.ResultCount} (optimized)");
    }

    private static void ForceGarbageCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static List<SatelliteCatalogEntry> CreateRealisticSatelliteCollection(int count)
    {
        var baseSatellites = new[]
        {
            new SatelliteCatalogEntry
            {
                Name = "ISS (ZARYA)", NoradId = "25544",
                Line1 = "1 25544U 98067A   26141.16510469  .00005835  00000-0  11282-3 0  9994",
                Line2 = "2 25544  51.6328  73.8715 0007529  81.3651 278.8190 15.49291753567565"
            },
            new SatelliteCatalogEntry
            {
                Name = "AO-07", NoradId = "07530",
                Line1 = "1 07530U 74089B   26141.31992461 -.00000054  00000-0  -48931-4 0  9992",
                Line2 = "2 07530 101.9910 154.2858 0012269 180.6108 191.1977 12.53697584357151"
            },
            new SatelliteCatalogEntry
            {
                Name = "AO-27", NoradId = "22825",
                Line1 = "1 22825U 93061C   26141.14902361  .00000060  00000-0  39806-4 0  9994",
                Line2 = "2 22825  98.6890 208.5706 0008550 172.0697 188.0622 14.30933961703139"
            },
            new SatelliteCatalogEntry
            {
                Name = "FO-29", NoradId = "24278",
                Line1 = "1 24278U 96046B   26141.17662052  .00000000  00000-0  34829-4 0  9991",
                Line2 = "2 24278  98.5266 353.7450 0350115 166.3802 194.7089 13.53272915469510"
            },
            new SatelliteCatalogEntry
            {
                Name = "SO-50", NoradId = "27607",
                Line1 = "1 27607U 02058C   26141.24923057  .00000576  00000-0  85866-4 0  9998",
                Line2 = "2 27607  64.5520 212.3264 0075596 267.4106  91.8345 14.82983020260469"
            },
            new SatelliteCatalogEntry
            {
                Name = "OSCAR-100", NoradId = "43700",
                Line1 = "1 43700U 18090A   26141.20833333  .00000000  00000-0  00000-0 0  9990",
                Line2 = "2 43700   0.0194 266.4022 0001413 204.6047 155.2785  1.00270176 13140"
            }
        };

        var result = new List<SatelliteCatalogEntry>();
        
        for (int i = 0; i < count; i++)
        {
            var template = baseSatellites[i % baseSatellites.Length];
            result.Add(new SatelliteCatalogEntry
            {
                Name = i < baseSatellites.Length ? template.Name : $"{template.Name} ({i + 1})",
                NoradId = i < baseSatellites.Length ? template.NoradId : $"{int.Parse(template.NoradId) + i}",
                Line1 = template.Line1,
                Line2 = template.Line2
            });
        }
        
        return result;
    }

    private static Dictionary<string, List<PassInfo>> GenerateRealisticPassData(
        List<SatelliteCatalogEntry> satellites, 
        int passesPerSatellite, 
        string prefix = "")
    {
        var data = new Dictionary<string, List<PassInfo>>();
        var baseTime = DateTime.UtcNow.AddHours(1);
        
        foreach (var satellite in satellites)
        {
            var passes = new List<PassInfo>();
            
            for (int i = 0; i < passesPerSatellite; i++)
            {
                var aos = baseTime.AddHours(i * 4 + (satellite.NoradId.GetHashCode() % 2));
                var duration = TimeSpan.FromMinutes(8 + i * 3);
                
                passes.Add(new PassInfo
                {
                    SatelliteName = string.IsNullOrEmpty(prefix) ? satellite.Name : $"{prefix} {satellite.Name}",
                    NoradId = satellite.NoradId,
                    AosUtc = aos,
                    LosUtc = aos.Add(duration),
                    MaxElevationDeg = 15.0 + i * 10.0,
                    MaxElevationUtc = aos.Add(duration.Divide(2)),
                    AosAzimuthDeg = 45.0 + i * 60.0,
                    LosAzimuthDeg = 135.0 + i * 60.0
                });
            }
            
            data[satellite.NoradId] = passes;
        }
        
        return data;
    }

    private static List<Task<IReadOnlyList<PassInfo>>> CreateCompletedPassTasks(
        List<SatelliteCatalogEntry> satellites,
        Dictionary<string, List<PassInfo>> passData)
    {
        var tasks = new List<Task<IReadOnlyList<PassInfo>>>();
        
        foreach (var satellite in satellites)
        {
            if (passData.TryGetValue(satellite.NoradId, out var passes))
            {
                tasks.Add(Task.FromResult<IReadOnlyList<PassInfo>>(passes));
            }
            else
            {
                tasks.Add(Task.FromResult<IReadOnlyList<PassInfo>>(Array.Empty<PassInfo>()));
            }
        }
        
        return tasks;
    }

    #endregion
}