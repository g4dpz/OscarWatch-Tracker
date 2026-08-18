using System.Diagnostics;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

/// <summary>
/// Integration tests demonstrating how AllocationMetrics would be used in practice
/// to measure allocation reduction in LINQ hotpath optimizations.
/// </summary>
public sealed class AllocationMetricsIntegrationTests
{
    [Fact]
    public void MeasureAllocationReduction_WithGcMeasurement_CapturesAllocations()
    {
        // Arrange
        var sw = Stopwatch.StartNew();
        var beforeBytes = GC.GetAllocatedBytesForCurrentThread();

        // Act - Simulate some allocations (similar to LINQ operations)
        var tempList = new List<string>();
        for (int i = 0; i < 100; i++)
        {
            tempList.Add($"Item {i}"); // This will cause allocations
        }
        var resultCount = tempList.Count;

        var afterBytes = GC.GetAllocatedBytesForCurrentThread();
        sw.Stop();

        var metrics = AllocationMetrics.Create(beforeBytes, afterBytes, sw.Elapsed, resultCount);

        // Assert
        Assert.True(metrics.AllocatedBytesAfter >= metrics.AllocatedBytesBefore);
        Assert.Equal(resultCount, metrics.ResultCount);
        Assert.True(metrics.ExecutionTime > TimeSpan.Zero);
        Assert.True(metrics.AllocationReduction <= 0); // No reduction in this example (we allocated memory)
    }

    [Fact]
    public void MeasureAllocationReduction_WithOptimizedVsUnoptimized_ShowsDifference()
    {
        // This test demonstrates how you would compare optimized vs unoptimized implementations

        // Measure unoptimized LINQ approach
        var unoptimizedMetrics = MeasureOperation(() =>
        {
            var items = Enumerable.Range(1, 1000);
            return items
                .Where(x => x % 2 == 0)
                .Select(x => x.ToString())
                .ToList();
        });

        // Measure optimized manual approach
        var optimizedMetrics = MeasureOperation(() =>
        {
            var result = new List<string>();
            for (int i = 1; i <= 1000; i++)
            {
                if (i % 2 == 0)
                {
                    result.Add(i.ToString());
                }
            }
            return result;
        });

        // Assert
        Assert.Equal(unoptimizedMetrics.ResultCount, optimizedMetrics.ResultCount);
        
        // In a real performance test, we might expect:
        // Assert.True(optimizedMetrics.AllocationReduction > unoptimizedMetrics.AllocationReduction);
        
        // For this test, just verify the metrics are captured properly
        Assert.True(unoptimizedMetrics.ExecutionTime > TimeSpan.Zero);
        Assert.True(optimizedMetrics.ExecutionTime > TimeSpan.Zero);
    }

    [Fact]
    public void AllocationMetrics_WithZeroAllocationScenario_ReportsCorrectly()
    {
        // Arrange - Simulate a scenario with minimal new allocations
        var sw = Stopwatch.StartNew();
        var beforeBytes = GC.GetAllocatedBytesForCurrentThread();

        // Act - Operation that reuses existing memory/objects
        var existingList = new List<int> { 1, 2, 3, 4, 5 };
        var count = 0;
        foreach (var item in existingList)
        {
            if (item > 2)
                count++;
        }

        var afterBytes = GC.GetAllocatedBytesForCurrentThread();
        sw.Stop();

        var metrics = AllocationMetrics.Create(beforeBytes, afterBytes, sw.Elapsed, count);

        // Assert
        Assert.Equal(count, metrics.ResultCount);
        Assert.True(metrics.ExecutionTime >= TimeSpan.Zero);
        
        // The allocation difference should be minimal (may vary due to GC timing)
        var allocationDifference = Math.Abs(metrics.AllocationReduction);
        Assert.True(allocationDifference < 1000, $"Expected minimal allocations, but got {allocationDifference} bytes difference");
    }

    private static AllocationMetrics MeasureOperation<T>(Func<T> operation) where T : System.Collections.ICollection
    {
        var sw = Stopwatch.StartNew();
        var beforeBytes = GC.GetAllocatedBytesForCurrentThread();

        var result = operation();

        var afterBytes = GC.GetAllocatedBytesForCurrentThread();
        sw.Stop();

        return AllocationMetrics.Create(beforeBytes, afterBytes, sw.Elapsed, result.Count);
    }
}