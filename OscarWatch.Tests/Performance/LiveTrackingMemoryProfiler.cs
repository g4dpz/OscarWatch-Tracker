using System.Diagnostics;
using System.Collections.Concurrent;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
using Xunit.Abstractions;

namespace OscarWatch.Tests.Performance;

/// <summary>
/// Memory profiling infrastructure for live tracking scenarios.
/// 
/// Provides continuous memory allocation monitoring during simulated tracking sessions
/// to detect memory pressure patterns and validate LINQ hotpath optimizations under
/// realistic workloads. Profiles memory usage during:
/// 
/// - Continuous pass predictions (every 250ms as per TrackingOrchestrator design)
/// - Mutual pass calculations for dual-site tracking
/// - Satellite addition/removal operations
/// - Peak workload scenarios with multiple concurrent operations
/// 
/// Results help identify memory allocation patterns, GC pressure points, and validate
/// that optimizations maintain effectiveness during sustained operation.
/// </summary>
[Trait("Category", "Performance")]
[Trait("Category", "MemoryProfiling")]
public sealed class LiveTrackingMemoryProfiler
{
    private readonly ITestOutputHelper _output;

    public LiveTrackingMemoryProfiler(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Memory profile for sustained pass prediction operations simulating live tracking.
    /// Measures allocation patterns during continuous 250ms prediction cycles.
    /// </summary>
    [Fact]
    public async Task ProfileContinuousPassPredictions_LiveTrackingScenario()
    {
        // Arrange: Simulate realistic live tracking scenario
        const int trackingDurationMinutes = 2; // 2-minute tracking session
        const int predictionIntervalMs = 250; // 250ms as per TrackingOrchestrator design
        const int satelliteCount = 8; // Realistic amateur satellite count
        
        var satellites = CreateRealisticSatelliteCollection(satelliteCount);
        var passData = GenerateRealisticPassData(satellites, passesPerSatellite: 5);
        
        var profiler = new MemoryProfiler();
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(trackingDurationMinutes)).Token;
        
        _output.WriteLine($"Starting continuous pass prediction profiling:");
        _output.WriteLine($"  Duration: {trackingDurationMinutes} minutes");
        _output.WriteLine($"  Interval: {predictionIntervalMs}ms");
        _output.WriteLine($"  Satellites: {satelliteCount}");
        _output.WriteLine($"  Expected cycles: ~{trackingDurationMinutes * 60 * 1000 / predictionIntervalMs}");
        
        // Act: Run continuous prediction cycles with memory profiling
        await profiler.ProfileAsync("ContinuousPassPredictions", async () =>
        {
            var cycleCount = 0;
            var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(predictionIntervalMs));
            
            try
            {
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    // Simulate GetPassesAsync call with realistic data
                    await SimulateGetPassesAsync(satellites, passData);
                    cycleCount++;
                    
                    if (cycleCount % 20 == 0) // Log every 5 seconds (20 cycles * 250ms)
                    {
                        _output.WriteLine($"  Completed {cycleCount} prediction cycles...");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when duration completes
            }
            
            _output.WriteLine($"Completed {cycleCount} prediction cycles");
            return cycleCount;
        });
        
        // Assert: Analyze memory usage patterns
        var report = profiler.GenerateReport();
        LogMemoryProfileReport("Continuous Pass Predictions", report);
        
        // Validate memory usage stays within reasonable bounds
        Assert.True(report.PeakMemoryUsage < 50 * 1024 * 1024, // 50MB peak usage limit
            $"Peak memory usage exceeded 50MB: {report.PeakMemoryUsage:N0} bytes");
        
        Assert.True(report.AverageAllocationRate < 1024 * 1024, // 1MB/s average allocation rate limit  
            $"Average allocation rate exceeded 1MB/s: {report.AverageAllocationRate:N0} bytes/s");
    }

    /// <summary>
    /// Memory profile for dual-site mutual pass tracking scenarios.
    /// Measures allocation patterns during concurrent local and remote pass calculations.
    /// </summary>
    [Fact]
    public async Task ProfileMutualPassTracking_DualSiteScenario()
    {
        // Arrange: Simulate dual-site tracking scenario
        const int trackingDurationMinutes = 1;
        const int predictionIntervalMs = 500; // Slightly longer interval for mutual passes
        const int satelliteCount = 6;
        
        var satellites = CreateRealisticSatelliteCollection(satelliteCount);
        var localPassData = GenerateRealisticPassData(satellites, passesPerSatellite: 4, prefix: "Local");
        var remotePassData = GenerateRealisticPassData(satellites, passesPerSatellite: 4, prefix: "Remote");
        
        var profiler = new MemoryProfiler();
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(trackingDurationMinutes)).Token;
        
        _output.WriteLine($"Starting mutual pass tracking profiling:");
        _output.WriteLine($"  Duration: {trackingDurationMinutes} minutes");
        _output.WriteLine($"  Interval: {predictionIntervalMs}ms");
        _output.WriteLine($"  Satellites: {satelliteCount}");
        _output.WriteLine($"  Sites: Local + Remote");
        
        // Act: Run mutual pass calculations with memory profiling
        await profiler.ProfileAsync("MutualPassTracking", async () =>
        {
            var cycleCount = 0;
            var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(predictionIntervalMs));
            
            try
            {
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    // Simulate GetMutualPassesAsync call
                    await SimulateGetMutualPassesAsync(satellites, localPassData, remotePassData);
                    cycleCount++;
                    
                    if (cycleCount % 10 == 0)
                    {
                        _output.WriteLine($"  Completed {cycleCount} mutual pass cycles...");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when duration completes
            }
            
            _output.WriteLine($"Completed {cycleCount} mutual pass cycles");
            return cycleCount;
        });
        
        // Assert: Analyze dual-site memory usage
        var report = profiler.GenerateReport();
        LogMemoryProfileReport("Mutual Pass Tracking", report);
        
        // Mutual pass calculations should have higher but still bounded memory usage
        Assert.True(report.PeakMemoryUsage < 75 * 1024 * 1024, // 75MB peak for dual-site
            $"Mutual pass peak memory usage exceeded 75MB: {report.PeakMemoryUsage:N0} bytes");
        
        Assert.True(report.AverageAllocationRate < 1.5 * 1024 * 1024, // 1.5MB/s for dual-site
            $"Mutual pass allocation rate exceeded 1.5MB/s: {report.AverageAllocationRate:N0} bytes/s");
    }

    /// <summary>
    /// Memory profile for satellite management operations during live tracking.
    /// Measures allocation impact of adding/removing satellites during active tracking.
    /// </summary>
    [Fact]
    public async Task ProfileSatelliteManagement_LiveOperations()
    {
        // Arrange: Simulate satellite management during tracking
        const int operationCount = 50;
        var satellites = CreateRealisticSatelliteCollection(20); // Larger pool for management ops
        
        var profiler = new MemoryProfiler();
        
        _output.WriteLine($"Starting satellite management profiling:");
        _output.WriteLine($"  Operations: {operationCount}");
        _output.WriteLine($"  Satellite pool: {satellites.Count}");
        
        // Act: Simulate realistic satellite add/remove patterns
        await profiler.ProfileAsync("SatelliteManagement", async () =>
        {
            var activeSatellites = new List<SatelliteCatalogEntry>(satellites.Take(10));
            var operationsMade = 0;
            
            for (int i = 0; i < operationCount; i++)
            {
                if (i % 2 == 0 && activeSatellites.Count < 15)
                {
                    // Add satellite operation
                    var candidateSatellites = satellites.Except(activeSatellites).ToList();
                    if (candidateSatellites.Count > 0)
                    {
                        var newSatellite = candidateSatellites[i % candidateSatellites.Count];
                        activeSatellites.Add(newSatellite);
                        operationsMade++;
                    }
                }
                else if (activeSatellites.Count > 5)
                {
                    // Remove satellite operation (simulate RemoveSatellite optimization)
                    var targetIndex = i % activeSatellites.Count;
                    var targetNoradId = activeSatellites[targetIndex].NoradId;
                    
                    // Simulate optimized RemoveSatellite operation
                    var filteredSatellites = new List<SatelliteCatalogEntry>(activeSatellites.Count - 1);
                    foreach (var sat in activeSatellites)
                    {
                        if (sat.NoradId != targetNoradId)
                        {
                            filteredSatellites.Add(sat);
                        }
                    }
                    activeSatellites = filteredSatellites;
                    operationsMade++;
                }
                
                // Simulate small delay between operations
                await Task.Delay(10);
                
                if (i % 10 == 0)
                {
                    _output.WriteLine($"  Completed {i + 1} management operations, active satellites: {activeSatellites.Count}");
                }
            }
            
            return operationsMade;
        });
        
        // Assert: Validate management operation memory efficiency
        var report = profiler.GenerateReport();
        LogMemoryProfileReport("Satellite Management", report);
        
        // Management operations should have minimal memory impact
        Assert.True(report.AverageAllocationRate < 512 * 1024, // 512KB/s for management ops
            $"Satellite management allocation rate exceeded 512KB/s: {report.AverageAllocationRate:N0} bytes/s");
    }

    /// <summary>
    /// Peak workload memory profiling combining all operations simultaneously.
    /// Simulates worst-case memory usage during high-activity tracking periods.
    /// </summary>
    [Fact]
    public async Task ProfilePeakWorkload_AllOperationsCombined()
    {
        // Arrange: Peak workload scenario
        const int durationMinutes = 1;
        const int satelliteCount = 12;
        
        var satellites = CreateRealisticSatelliteCollection(satelliteCount);
        var passData = GenerateRealisticPassData(satellites, passesPerSatellite: 6);
        var localPassData = GenerateRealisticPassData(satellites, passesPerSatellite: 4, prefix: "Local");
        var remotePassData = GenerateRealisticPassData(satellites, passesPerSatellite: 4, prefix: "Remote");
        
        var profiler = new MemoryProfiler();
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(durationMinutes)).Token;
        
        _output.WriteLine($"Starting peak workload profiling:");
        _output.WriteLine($"  Duration: {durationMinutes} minutes");
        _output.WriteLine($"  Satellites: {satelliteCount}");
        _output.WriteLine($"  Operations: Pass predictions + Mutual passes + Management");
        
        // Act: Run combined peak workload
        await profiler.ProfileAsync("PeakWorkload", async () =>
        {
            var operations = 0;
            var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200)); // Aggressive 200ms cycle
            
            try
            {
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    // Combine multiple operations per cycle
                    var tasks = new List<Task>
                    {
                        SimulateGetPassesAsync(satellites, passData),
                        SimulateGetMutualPassesAsync(satellites, localPassData, remotePassData)
                    };
                    
                    // Occasionally add satellite management operations
                    if (operations % 5 == 0)
                    {
                        tasks.Add(Task.Run(() => SimulateRemoveSatellite(satellites)));
                    }
                    
                    await Task.WhenAll(tasks);
                    operations++;
                    
                    if (operations % 25 == 0)
                    {
                        _output.WriteLine($"  Completed {operations} peak workload cycles...");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when duration completes
            }
            
            _output.WriteLine($"Completed {operations} peak workload cycles");
            return operations;
        });
        
        // Assert: Validate peak workload memory bounds
        var report = profiler.GenerateReport();
        LogMemoryProfileReport("Peak Workload", report);
        
        // Peak workload should stay within elevated but reasonable limits
        Assert.True(report.PeakMemoryUsage < 100 * 1024 * 1024, // 100MB peak for combined workload
            $"Peak workload memory usage exceeded 100MB: {report.PeakMemoryUsage:N0} bytes");
        
        Assert.True(report.AverageAllocationRate < 2 * 1024 * 1024, // 2MB/s for peak workload
            $"Peak workload allocation rate exceeded 2MB/s: {report.AverageAllocationRate:N0} bytes/s");
    }

    #region Simulation Methods

    private async Task SimulateGetPassesAsync(
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
        
        await Task.WhenAll(tasks);
        
        // Simulate optimized GetPassesAsync processing
        var minDuration = TimeSpan.FromMinutes(0);
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
    }

    private async Task SimulateGetMutualPassesAsync(
        List<SatelliteCatalogEntry> satellites,
        Dictionary<string, List<PassInfo>> localPassData,
        Dictionary<string, List<PassInfo>> remotePassData)
    {
        var localTasks = new List<Task<IReadOnlyList<PassInfo>>>();
        var remoteTasks = new List<Task<IReadOnlyList<PassInfo>>>();
        
        foreach (var satellite in satellites)
        {
            if (localPassData.TryGetValue(satellite.NoradId, out var localPasses))
                localTasks.Add(Task.FromResult<IReadOnlyList<PassInfo>>(localPasses));
            else
                localTasks.Add(Task.FromResult<IReadOnlyList<PassInfo>>(Array.Empty<PassInfo>()));
                
            if (remotePassData.TryGetValue(satellite.NoradId, out var remotePasses))
                remoteTasks.Add(Task.FromResult<IReadOnlyList<PassInfo>>(remotePasses));
            else
                remoteTasks.Add(Task.FromResult<IReadOnlyList<PassInfo>>(Array.Empty<PassInfo>()));
        }
        
        await Task.WhenAll(localTasks.Concat(remoteTasks));
        
        // Simulate optimized GetMutualPassesAsync processing
        var minDuration = TimeSpan.FromMinutes(0);
        var localResults = HotPathCollections.GetLocalPassBuffer();
        var remoteResults = HotPathCollections.GetRemotePassBuffer();
        
        foreach (var task in localTasks)
        {
            if (task.IsCompletedSuccessfully)
            {
                foreach (var pass in task.Result)
                {
                    if (pass.Duration >= minDuration)
                    {
                        localResults.Add(pass);
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
                        remoteResults.Add(pass);
                    }
                }
            }
        }
    }

    private void SimulateRemoveSatellite(List<SatelliteCatalogEntry> satellites)
    {
        if (satellites.Count == 0) return;
        
        var targetNoradId = satellites[Random.Shared.Next(satellites.Count)].NoradId;
        
        // Simulate optimized RemoveSatellite operation
        var filtered = new List<SatelliteCatalogEntry>(satellites.Count - 1);
        foreach (var satellite in satellites)
        {
            if (satellite.NoradId != targetNoradId)
            {
                filtered.Add(satellite);
            }
        }
    }

    #endregion

    #region Helper Methods

    private void LogMemoryProfileReport(string scenario, MemoryProfileReport report)
    {
        _output.WriteLine($"=== {scenario} Memory Profile Report ===");
        _output.WriteLine($"Duration: {report.ProfileDuration.TotalSeconds:F1}s");
        _output.WriteLine($"Peak Memory: {report.PeakMemoryUsage / (1024 * 1024.0):F1} MB");
        _output.WriteLine($"Average Allocation Rate: {report.AverageAllocationRate / 1024.0:F1} KB/s");
        _output.WriteLine($"Total Allocations: {report.TotalAllocations / (1024 * 1024.0):F1} MB");
        _output.WriteLine($"GC Collections: Gen0={report.GcGen0Collections}, Gen1={report.GcGen1Collections}, Gen2={report.GcGen2Collections}");
        _output.WriteLine($"Sample Count: {report.SampleCount}");
        
        if (report.AllocationSpikes.Count > 0)
        {
            _output.WriteLine($"Allocation Spikes: {report.AllocationSpikes.Count}");
            foreach (var spike in report.AllocationSpikes.Take(5)) // Show first 5 spikes
            {
                _output.WriteLine($"  - {spike.Timestamp:HH:mm:ss.fff}: {spike.AllocationBytes / 1024.0:F1} KB");
            }
        }
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

    #endregion
}

/// <summary>
/// Memory profiler for tracking allocation patterns and GC pressure during operation execution.
/// </summary>
internal sealed class MemoryProfiler
{
    private readonly List<MemorySample> _samples = new();
    private readonly Stopwatch _stopwatch = new();
    private readonly Timer? _samplingTimer;
    private long _initialAllocations;
    private long _peakMemoryUsage;
    private int _initialGen0Collections;
    private int _initialGen1Collections;
    private int _initialGen2Collections;

    public MemoryProfiler(TimeSpan? samplingInterval = null)
    {
        var interval = samplingInterval ?? TimeSpan.FromMilliseconds(100); // 100ms sampling by default
        _samplingTimer = new Timer(TakeSample, null, Timeout.Infinite, Timeout.Infinite);
    }

    public async Task<T> ProfileAsync<T>(string operationName, Func<Task<T>> operation)
    {
        StartProfiling(operationName);
        try
        {
            return await operation();
        }
        finally
        {
            StopProfiling();
        }
    }

    public void StartProfiling(string operationName)
    {
        // Initialize baseline measurements
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        _initialAllocations = GC.GetAllocatedBytesForCurrentThread();
        _peakMemoryUsage = GC.GetTotalMemory(false);
        _initialGen0Collections = GC.CollectionCount(0);
        _initialGen1Collections = GC.CollectionCount(1);
        _initialGen2Collections = GC.CollectionCount(2);
        
        _samples.Clear();
        _stopwatch.Restart();
        
        // Start sampling timer
        _samplingTimer?.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    }

    public void StopProfiling()
    {
        _samplingTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _stopwatch.Stop();
        
        // Take final sample
        TakeSample(null);
    }

    public MemoryProfileReport GenerateReport()
    {
        var duration = _stopwatch.Elapsed;
        var totalAllocations = GC.GetAllocatedBytesForCurrentThread() - _initialAllocations;
        var averageAllocationRate = duration.TotalSeconds > 0 ? totalAllocations / duration.TotalSeconds : 0;
        
        var gen0Collections = GC.CollectionCount(0) - _initialGen0Collections;
        var gen1Collections = GC.CollectionCount(1) - _initialGen1Collections;
        var gen2Collections = GC.CollectionCount(2) - _initialGen2Collections;
        
        // Detect allocation spikes (samples with >10KB allocation in 100ms interval)
        var spikes = _samples
            .Where(s => s.AllocationDelta > 10 * 1024)
            .Select(s => new AllocationSpike
            {
                Timestamp = DateTime.UtcNow.Subtract(duration).Add(TimeSpan.FromTicks(s.ElapsedTicks)),
                AllocationBytes = s.AllocationDelta
            })
            .ToList();
        
        return new MemoryProfileReport
        {
            ProfileDuration = duration,
            PeakMemoryUsage = _peakMemoryUsage,
            TotalAllocations = totalAllocations,
            AverageAllocationRate = averageAllocationRate,
            GcGen0Collections = gen0Collections,
            GcGen1Collections = gen1Collections,
            GcGen2Collections = gen2Collections,
            SampleCount = _samples.Count,
            AllocationSpikes = spikes
        };
    }

    private void TakeSample(object? state)
    {
        var currentMemory = GC.GetTotalMemory(false);
        var currentAllocations = GC.GetAllocatedBytesForCurrentThread();
        
        if (currentMemory > _peakMemoryUsage)
            _peakMemoryUsage = currentMemory;
        
        var previousAllocations = _samples.Count > 0 
            ? _samples[^1].TotalAllocations 
            : _initialAllocations;
        
        _samples.Add(new MemorySample
        {
            ElapsedTicks = _stopwatch.ElapsedTicks,
            TotalMemory = currentMemory,
            TotalAllocations = currentAllocations,
            AllocationDelta = currentAllocations - previousAllocations
        });
    }
}

/// <summary>
/// Memory profiling report containing allocation patterns and GC statistics.
/// </summary>
internal sealed record MemoryProfileReport
{
    public TimeSpan ProfileDuration { get; init; }
    public long PeakMemoryUsage { get; init; }
    public long TotalAllocations { get; init; }
    public double AverageAllocationRate { get; init; }
    public int GcGen0Collections { get; init; }
    public int GcGen1Collections { get; init; }
    public int GcGen2Collections { get; init; }
    public int SampleCount { get; init; }
    public List<AllocationSpike> AllocationSpikes { get; init; } = new();
}

/// <summary>
/// Represents a memory allocation spike detected during profiling.
/// </summary>
internal sealed record AllocationSpike
{
    public DateTime Timestamp { get; init; }
    public long AllocationBytes { get; init; }
}

/// <summary>
/// Individual memory sample taken during profiling.
/// </summary>
internal sealed record MemorySample
{
    public long ElapsedTicks { get; init; }
    public long TotalMemory { get; init; }
    public long TotalAllocations { get; init; }
    public long AllocationDelta { get; init; }
}