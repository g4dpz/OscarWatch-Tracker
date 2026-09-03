using System.Diagnostics;

namespace OscarWatch.Tests.Performance;

/// <summary>
/// Service for measuring memory allocations during tracking loop operations
/// to validate the effectiveness of memory optimizations.
/// </summary>
public sealed class AllocationTrackingService
{
    private long _beforeAllocations;
    private long _afterAllocations;
    private Stopwatch _stopwatch = new();
    private int _operationCount;

    /// <summary>
    /// Start measuring allocations for the current thread.
    /// </summary>
    public void StartMeasurement()
    {
        // Force GC to get accurate baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        _beforeAllocations = GC.GetAllocatedBytesForCurrentThread();
        _stopwatch.Restart();
        _operationCount = 0;
    }
    
    /// <summary>
    /// Stop measuring and record the final allocation count.
    /// </summary>
    public void StopMeasurement()
    {
        _afterAllocations = GC.GetAllocatedBytesForCurrentThread();
        _stopwatch.Stop();
    }
    
    /// <summary>
    /// Increment the operation count (e.g., tracking ticks processed).
    /// </summary>
    public void IncrementOperationCount()
    {
        _operationCount++;
    }
    
    /// <summary>
    /// Get the allocation measurement results.
    /// </summary>
    public AllocationMeasurement GetMeasurement()
    {
        return new AllocationMeasurement
        {
            TotalAllocatedBytes = _afterAllocations - _beforeAllocations,
            ElapsedMilliseconds = _stopwatch.ElapsedMilliseconds,
            OperationCount = _operationCount,
            AllocatedBytesPerOperation = _operationCount > 0 ? (_afterAllocations - _beforeAllocations) / (double)_operationCount : 0
        };
    }
}

/// <summary>
/// Results of an allocation measurement session.
/// </summary>
public sealed class AllocationMeasurement
{
    public long TotalAllocatedBytes { get; init; }
    public long ElapsedMilliseconds { get; init; }
    public int OperationCount { get; init; }
    public double AllocatedBytesPerOperation { get; init; }
    
    /// <summary>
    /// Calculate the percentage reduction compared to a baseline measurement.
    /// </summary>
    public double CalculateReductionPercentage(AllocationMeasurement baseline)
    {
        if (baseline.AllocatedBytesPerOperation <= 0)
            return 0.0;
            
        var reduction = baseline.AllocatedBytesPerOperation - AllocatedBytesPerOperation;
        return (reduction / baseline.AllocatedBytesPerOperation) * 100.0;
    }
    
    /// <summary>
    /// Get a human-readable summary of the allocation measurement.
    /// </summary>
    public override string ToString()
    {
        return $"Total: {TotalAllocatedBytes:N0} bytes, " +
               $"Per-op: {AllocatedBytesPerOperation:N1} bytes, " +
               $"Operations: {OperationCount}, " +
               $"Time: {ElapsedMilliseconds}ms";
    }
}