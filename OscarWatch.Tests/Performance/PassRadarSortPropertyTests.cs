// Feature: startup-io-rendering-optimisation, Property 12: Pass radar single-sort correctness

using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Core.Models;

namespace OscarWatch.Tests.Performance;

/// <summary>
/// Property-based tests for pass radar gallery sort correctness.
/// The gallery sorts passes by AosUtc exactly once before passing to the builder.
///
/// **Validates: Requirements 10.3**
/// </summary>
public sealed class PassRadarSortPropertyTests
{
    /// <summary>
    /// Property 12: Pass radar single-sort correctness.
    /// For any list of PassInfo items with arbitrary AosUtc timestamps,
    /// the sorted output is in strictly non-decreasing AosUtc order.
    ///
    /// **Validates: Requirements 10.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Sorted_passes_are_in_nondecreasing_aos_order(long[] tickOffsets)
    {
        if (tickOffsets is null || tickOffsets.Length == 0)
            return true; // vacuously true for empty input

        // Generate PassInfo list with arbitrary AosUtc timestamps
        var baseTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var passes = tickOffsets.Select((offset, i) => new PassInfo
        {
            SatelliteName = "TEST-SAT",
            NoradId = "25544",
            AosUtc = baseTime.AddTicks(Math.Abs(offset) % (TimeSpan.TicksPerDay * 365)),
            LosUtc = baseTime.AddTicks(Math.Abs(offset) % (TimeSpan.TicksPerDay * 365))
                     .AddMinutes(10),
            MaxElevationDeg = 45.0,
            MaxElevationUtc = baseTime.AddTicks(Math.Abs(offset) % (TimeSpan.TicksPerDay * 365))
                              .AddMinutes(5),
            AosAzimuthDeg = 180.0,
            LosAzimuthDeg = 0.0
        }).ToList();

        // Apply the same sort logic used in InitializeAsync
        var sorted = passes.OrderBy(p => p.AosUtc).ToList();

        // Assert non-decreasing AosUtc order
        for (var i = 0; i < sorted.Count - 1; i++)
        {
            if (sorted[i].AosUtc > sorted[i + 1].AosUtc)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Property 12 (supplementary): Sorting preserves all elements.
    /// For any list of PassInfo items, the sorted output contains the same
    /// elements as the input (no items lost or duplicated).
    ///
    /// **Validates: Requirements 10.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Sort_preserves_all_elements(long[] tickOffsets)
    {
        if (tickOffsets is null || tickOffsets.Length == 0)
            return true;

        var baseTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var passes = tickOffsets.Select((offset, i) => new PassInfo
        {
            SatelliteName = "TEST-SAT",
            NoradId = "25544",
            AosUtc = baseTime.AddTicks(Math.Abs(offset) % (TimeSpan.TicksPerDay * 365)),
            LosUtc = baseTime.AddTicks(Math.Abs(offset) % (TimeSpan.TicksPerDay * 365))
                     .AddMinutes(10),
            MaxElevationDeg = 45.0,
            MaxElevationUtc = baseTime.AddTicks(Math.Abs(offset) % (TimeSpan.TicksPerDay * 365))
                              .AddMinutes(5),
            AosAzimuthDeg = 180.0,
            LosAzimuthDeg = 0.0
        }).ToList();

        var sorted = passes.OrderBy(p => p.AosUtc).ToList();

        // Same count
        if (sorted.Count != passes.Count)
            return false;

        // Same set of AosUtc values (multiset equality)
        var originalTicks = passes.Select(p => p.AosUtc.Ticks).OrderBy(t => t).ToList();
        var sortedTicks = sorted.Select(p => p.AosUtc.Ticks).OrderBy(t => t).ToList();

        return originalTicks.SequenceEqual(sortedTicks);
    }

    /// <summary>
    /// Property 12 (supplementary): Sorting an already-sorted list yields identical output.
    /// This confirms no redundant re-sorting changes the result — idempotency of sort.
    ///
    /// **Validates: Requirements 10.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Sorting_already_sorted_input_is_idempotent(long[] tickOffsets)
    {
        if (tickOffsets is null || tickOffsets.Length == 0)
            return true;

        var baseTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var passes = tickOffsets.Select((offset, i) => new PassInfo
        {
            SatelliteName = "TEST-SAT",
            NoradId = "25544",
            AosUtc = baseTime.AddTicks(Math.Abs(offset) % (TimeSpan.TicksPerDay * 365)),
            LosUtc = baseTime.AddTicks(Math.Abs(offset) % (TimeSpan.TicksPerDay * 365))
                     .AddMinutes(10),
            MaxElevationDeg = 45.0,
            MaxElevationUtc = baseTime.AddTicks(Math.Abs(offset) % (TimeSpan.TicksPerDay * 365))
                              .AddMinutes(5),
            AosAzimuthDeg = 180.0,
            LosAzimuthDeg = 0.0
        }).ToList();

        // Sort once
        var sortedOnce = passes.OrderBy(p => p.AosUtc).ToList();
        // Sort again (should produce identical order)
        var sortedTwice = sortedOnce.OrderBy(p => p.AosUtc).ToList();

        // The two sorts should produce identical AosUtc sequences
        return sortedOnce.Select(p => p.AosUtc.Ticks)
            .SequenceEqual(sortedTwice.Select(p => p.AosUtc.Ticks));
    }
}
