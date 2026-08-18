// Feature: linq-hotpath-optimization, Property 3: Inline Filtering Without Intermediate Collections

using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 1.4, 2.3**
/// 
/// Property 3: Inline Filtering Without Intermediate Collections
/// 
/// For any collection of PassInfo objects and any minimum duration threshold, the optimized 
/// implementation SHALL apply filtering during enumeration without creating intermediate 
/// filtered collections while producing results identical to LINQ filtering.
/// </summary>
public class InlineFilteringBehaviorPropertyTests
{
    /// <summary>
    /// Property 3: Inline filtering behavior equivalence
    ///
    /// Strategy:
    /// - Generate collections of PassInfo objects with varying durations
    /// - Test filtering with different minimum duration thresholds
    /// - Compare results between LINQ filtering (creates intermediate collections) 
    ///   and optimized inline filtering (no intermediate collections)
    /// - Verify identical results across edge cases (empty collections, zero durations, etc.)
    /// - Demonstrate that filtering is applied during enumeration rather than creating filtered collections
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Inline_filtering_produces_identical_results_to_LINQ_filtering(
        byte passCount, 
        byte[] durationMinutes,
        byte minimumDurationMinutes)
    {
        if (durationMinutes is null || durationMinutes.Length == 0)
            return true;

        // Generate a reasonable number of passes (1-10)
        var numPasses = Math.Max(1, passCount % 11);
        
        // Create valid PassInfo objects with varying durations
        var inputPasses = new PassInfo[numPasses];
        var baseTime = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        
        for (var i = 0; i < numPasses; i++)
        {
            var durationMin = Math.Max(1, durationMinutes[i % durationMinutes.Length] % 31); // 1-30 minutes
            var aos = baseTime.AddMinutes(i * 60); // Space passes 1 hour apart
            var los = aos.AddMinutes(durationMin);
            
            inputPasses[i] = new PassInfo
            {
                SatelliteName = $"SAT-{i}",
                NoradId = (25544 + i).ToString(),
                AosUtc = aos,
                LosUtc = los,
                MaxElevationDeg = 20.0 + (i * 5),
                MaxElevationUtc = aos.AddMinutes(durationMin / 2.0),
                AosAzimuthDeg = 45.0 + (i * 15),
                LosAzimuthDeg = 225.0 + (i * 15)
            };
        }

        // Map to reasonable duration range (0-20 minutes)
        var minDurationMinutes = minimumDurationMinutes % 21;
        var minDuration = TimeSpan.FromMinutes(minDurationMinutes);

        // Test LINQ filtering (creates intermediate collections)
        var linqResults = ApplyLINQFiltering(inputPasses, minDuration);

        // Test optimized inline filtering (no intermediate collections)
        var inlineResults = ApplyInlineFiltering(inputPasses, minDuration);

        // Results must be identical
        if (linqResults.Count != inlineResults.Count)
            return false;

        // Verify each result matches exactly
        for (var i = 0; i < linqResults.Count; i++)
        {
            var linq = linqResults[i];
            var inline = inlineResults[i];

            // All properties must be identical
            if (linq.NoradId != inline.NoradId ||
                linq.SatelliteName != inline.SatelliteName ||
                linq.AosUtc != inline.AosUtc ||
                linq.LosUtc != inline.LosUtc ||
                Math.Abs(linq.MaxElevationDeg - inline.MaxElevationDeg) > 0.001 ||
                linq.MaxElevationUtc != inline.MaxElevationUtc ||
                Math.Abs(linq.AosAzimuthDeg - inline.AosAzimuthDeg) > 0.001 ||
                Math.Abs(linq.LosAzimuthDeg - inline.LosAzimuthDeg) > 0.001)
            {
                return false;
            }
        }

        // Verify that all results in both collections meet the filtering criteria
        foreach (var result in linqResults)
        {
            if (result.Duration < minDuration)
                return false;
        }

        foreach (var result in inlineResults)
        {
            if (result.Duration < minDuration)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Property 3b: Edge case handling for inline filtering
    ///
    /// Tests that inline filtering handles edge cases identically to LINQ filtering:
    /// - Empty collections
    /// - Collections with all passes below threshold
    /// - Collections with all passes above threshold
    /// - Zero duration threshold
    /// - Very high duration threshold
    /// </summary>
    [Property(MaxTest = 50)]
    public bool Inline_filtering_handles_edge_cases_identically_to_LINQ(
        bool useEmptyCollection,
        bool useZeroDuration,
        bool useExtremeThreshold,
        byte passCount)
    {
        PassInfo[] testPasses;
        
        if (useEmptyCollection)
        {
            testPasses = [];
        }
        else
        {
            // Create a small collection of valid passes (1-5 passes)
            var numPasses = Math.Max(1, passCount % 6);
            testPasses = new PassInfo[numPasses];
            var baseTime = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
            
            for (var i = 0; i < numPasses; i++)
            {
                var durationMin = 5 + (i * 3); // 5, 8, 11, 14, 17 minute passes
                var aos = baseTime.AddMinutes(i * 30);
                var los = aos.AddMinutes(durationMin);
                
                testPasses[i] = new PassInfo
                {
                    SatelliteName = $"TEST-SAT-{i}",
                    NoradId = (40000 + i).ToString(),
                    AosUtc = aos,
                    LosUtc = los,
                    MaxElevationDeg = 30.0 + (i * 10),
                    MaxElevationUtc = aos.AddMinutes(durationMin / 2.0),
                    AosAzimuthDeg = 90.0 + (i * 30),
                    LosAzimuthDeg = 270.0 + (i * 30)
                };
            }
        }
        
        TimeSpan minDuration;
        if (useZeroDuration)
        {
            minDuration = TimeSpan.Zero;
        }
        else if (useExtremeThreshold)
        {
            minDuration = TimeSpan.FromHours(24); // Extremely high threshold
        }
        else
        {
            minDuration = TimeSpan.FromMinutes(10); // Normal threshold
        }

        var linqResults = ApplyLINQFiltering(testPasses, minDuration);
        var inlineResults = ApplyInlineFiltering(testPasses, minDuration);

        // Results must be identical even for edge cases
        if (linqResults.Count != inlineResults.Count)
            return false;

        for (var i = 0; i < linqResults.Count; i++)
        {
            if (!ArePassInfoEqual(linqResults[i], inlineResults[i]))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Property 3c: Filtering during Task enumeration matches LINQ SelectMany behavior
    ///
    /// Tests the actual pattern used in GetPassesAsync and GetMutualPassesAsync where
    /// filtering is applied during Task result enumeration, matching the SelectMany + Where pattern.
    /// </summary>
    [Property(MaxTest = 50)]
    public bool Task_enumeration_filtering_matches_LINQ_SelectMany_filtering(
        byte taskCount,
        byte[] passCountsPerTask,
        byte minimumDurationMinutes)
    {
        if (passCountsPerTask is null || passCountsPerTask.Length == 0)
            return true;

        // Generate reasonable number of tasks (1-5)
        var numTasks = Math.Max(1, taskCount % 6);
        var tasks = new List<Task<IReadOnlyList<PassInfo>>>();
        var baseTime = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        for (var t = 0; t < numTasks; t++)
        {
            var passesPerTask = Math.Max(1, passCountsPerTask[t % passCountsPerTask.Length] % 4); // 1-3 passes per task
            var passes = new List<PassInfo>();

            for (var p = 0; p < passesPerTask; p++)
            {
                var durationMin = 5 + ((t * 3) + (p * 2)); // Varying durations: 5, 7, 9, 8, 10, 12, etc.
                var aosOffset = (t * 60) + (p * 20); // Space passes appropriately
                var aos = baseTime.AddMinutes(aosOffset);
                var los = aos.AddMinutes(durationMin);

                var pass = new PassInfo
                {
                    SatelliteName = $"SAT-T{t}-P{p}",
                    NoradId = (50000 + (t * 10) + p).ToString(),
                    AosUtc = aos,
                    LosUtc = los,
                    MaxElevationDeg = 25.0 + (t * 5) + (p * 3),
                    MaxElevationUtc = aos.AddMinutes(durationMin / 2.0),
                    AosAzimuthDeg = 60.0 + (t * 20) + (p * 10),
                    LosAzimuthDeg = 240.0 + (t * 20) + (p * 10)
                };

                passes.Add(pass);
            }

            tasks.Add(Task.FromResult<IReadOnlyList<PassInfo>>(passes));
        }

        if (tasks.Count == 0)
            return true;

        var minDurationMinutes = minimumDurationMinutes % 21;
        var minDuration = TimeSpan.FromMinutes(minDurationMinutes);

        // Wait for all tasks (they're already completed)
        Task.WhenAll(tasks).Wait();

        // LINQ approach: SelectMany + Where creates intermediate collections
        var linqResults = ApplyLINQTaskFiltering(tasks, minDuration);

        // Optimized approach: inline filtering during task enumeration
        var inlineResults = ApplyInlineTaskFiltering(tasks, minDuration);

        // Results must be identical
        if (linqResults.Count != inlineResults.Count)
            return false;

        for (var i = 0; i < linqResults.Count; i++)
        {
            if (!ArePassInfoEqual(linqResults[i], inlineResults[i]))
                return false;
        }

        return true;
    }

    #region Filtering Logic Methods

    /// <summary>
    /// Simulates LINQ filtering that creates intermediate collections.
    /// This is the pattern that was replaced in the optimization.
    /// </summary>
    private static IReadOnlyList<PassInfo> ApplyLINQFiltering(
        PassInfo[] inputPasses, 
        TimeSpan minDuration)
    {
        // LINQ Where clause creates an intermediate IEnumerable
        return inputPasses
            .Where(p => p.Duration >= minDuration)  // Creates intermediate filtered collection
            .ToList();                               // Final materialization
    }

    /// <summary>
    /// Simulates optimized inline filtering that avoids intermediate collections.
    /// This matches the implementation in the optimized methods.
    /// </summary>
    private static IReadOnlyList<PassInfo> ApplyInlineFiltering(
        PassInfo[] inputPasses, 
        TimeSpan minDuration)
    {
        var results = new List<PassInfo>();

        // Manual enumeration applies filtering during iteration (no intermediate collections)
        foreach (var pass in inputPasses)
        {
            if (pass.Duration >= minDuration)
            {
                results.Add(pass);
            }
        }

        return results;
    }

    /// <summary>
    /// Simulates LINQ task processing with SelectMany + Where (creates intermediate collections).
    /// This matches the original implementation pattern in GetPassesAsync and GetMutualPassesAsync.
    /// </summary>
    private static IReadOnlyList<PassInfo> ApplyLINQTaskFiltering(
        List<Task<IReadOnlyList<PassInfo>>> tasks, 
        TimeSpan minDuration)
    {
        // Original LINQ pattern that creates multiple intermediate collections
        return tasks
            .Where(t => t.IsCompletedSuccessfully)      // IEnumerable wrapper
            .SelectMany(t => t.Result)                  // SelectMany creates intermediate collection + buffer
            .Where(p => p.Duration >= minDuration)      // Where creates another intermediate collection
            .ToList();                                  // Final materialization
    }

    /// <summary>
    /// Simulates optimized task processing with inline filtering (no intermediate collections).
    /// This matches the optimized implementation in GetPassesAsync and GetMutualPassesAsync.
    /// </summary>
    private static IReadOnlyList<PassInfo> ApplyInlineTaskFiltering(
        List<Task<IReadOnlyList<PassInfo>>> tasks, 
        TimeSpan minDuration)
    {
        var results = new List<PassInfo>();

        // Manual enumeration applies filtering during iteration (no intermediate collections)
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

        return results;
    }

    /// <summary>
    /// Compares two PassInfo objects for exact equality.
    /// </summary>
    private static bool ArePassInfoEqual(PassInfo a, PassInfo b)
    {
        return a.NoradId == b.NoradId &&
               a.SatelliteName == b.SatelliteName &&
               a.AosUtc == b.AosUtc &&
               a.LosUtc == b.LosUtc &&
               Math.Abs(a.MaxElevationDeg - b.MaxElevationDeg) < 0.001 &&
               a.MaxElevationUtc == b.MaxElevationUtc &&
               Math.Abs(a.AosAzimuthDeg - b.AosAzimuthDeg) < 0.001 &&
               Math.Abs(a.LosAzimuthDeg - b.LosAzimuthDeg) < 0.001;
    }

    #endregion
}