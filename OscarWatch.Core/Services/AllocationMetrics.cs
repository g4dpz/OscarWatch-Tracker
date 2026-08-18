namespace OscarWatch.Core.Services;

/// <summary>
/// Performance measurement class for tracking memory allocation reduction in LINQ hotpath optimizations.
/// Provides before/after allocation tracking, execution time measurement, and result counting capabilities
/// to validate the effectiveness of allocation-free implementations.
/// </summary>
public sealed class AllocationMetrics
{
    /// <summary>
    /// Gets the number of bytes allocated before the operation.
    /// </summary>
    public long AllocatedBytesBefore { get; init; }

    /// <summary>
    /// Gets the number of bytes allocated after the operation.
    /// </summary>
    public long AllocatedBytesAfter { get; init; }

    /// <summary>
    /// Gets the execution time of the operation.
    /// </summary>
    public TimeSpan ExecutionTime { get; init; }

    /// <summary>
    /// Gets the number of results returned by the operation.
    /// </summary>
    public int ResultCount { get; init; }

    /// <summary>
    /// Gets the reduction in allocated bytes (Before - After).
    /// Positive values indicate a reduction in allocations.
    /// </summary>
    public long AllocationReduction => AllocatedBytesBefore - AllocatedBytesAfter;

    /// <summary>
    /// Gets the percentage reduction in allocations relative to the before measurement.
    /// Returns 0.0 if no allocations were measured before the operation.
    /// </summary>
    public double ReductionPercentage => AllocatedBytesBefore > 0
        ? (double)AllocationReduction / AllocatedBytesBefore * 100.0
        : 0.0;

    /// <summary>
    /// Creates allocation metrics with the specified measurements.
    /// </summary>
    /// <param name="allocatedBytesBefore">Bytes allocated before the operation.</param>
    /// <param name="allocatedBytesAfter">Bytes allocated after the operation.</param>
    /// <param name="executionTime">Time taken to execute the operation.</param>
    /// <param name="resultCount">Number of results produced by the operation.</param>
    /// <returns>A new AllocationMetrics instance.</returns>
    public static AllocationMetrics Create(
        long allocatedBytesBefore,
        long allocatedBytesAfter,
        TimeSpan executionTime,
        int resultCount) =>
        new()
        {
            AllocatedBytesBefore = allocatedBytesBefore,
            AllocatedBytesAfter = allocatedBytesAfter,
            ExecutionTime = executionTime,
            ResultCount = resultCount
        };

    /// <summary>
    /// Creates allocation metrics indicating no allocations occurred.
    /// </summary>
    /// <param name="executionTime">Time taken to execute the operation.</param>
    /// <param name="resultCount">Number of results produced by the operation.</param>
    /// <returns>A new AllocationMetrics instance with zero allocations.</returns>
    public static AllocationMetrics NoAllocations(TimeSpan executionTime, int resultCount) =>
        new()
        {
            AllocatedBytesBefore = 0,
            AllocatedBytesAfter = 0,
            ExecutionTime = executionTime,
            ResultCount = resultCount
        };

    /// <summary>
    /// Returns a string representation of the allocation metrics for debugging and logging.
    /// </summary>
    public override string ToString()
    {
        if (AllocatedBytesBefore == 0 && AllocatedBytesAfter == 0)
        {
            return $"AllocationMetrics: No allocations, {ResultCount} results, {ExecutionTime.TotalMilliseconds:F2}ms";
        }

        return $"AllocationMetrics: {AllocationReduction:+#,##0;-#,##0;0} bytes ({ReductionPercentage:+0.0;-0.0;0.0}%), " +
               $"{ResultCount} results, {ExecutionTime.TotalMilliseconds:F2}ms";
    }
}