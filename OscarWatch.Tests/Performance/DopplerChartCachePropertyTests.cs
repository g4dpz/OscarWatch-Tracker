// Feature: startup-io-rendering-optimisation, Property 9: Doppler chart cached metrics equal LINQ-computed values
// Feature: startup-io-rendering-optimisation, Property 10: Doppler chart visible-window filter correctness

using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Controls;
using OscarWatch.ViewModels;

namespace OscarWatch.Tests.Performance;

/// <summary>
/// Property-based tests for DopplerTimeSeriesChartControl cached metrics and visible-window filtering.
///
/// **Validates: Requirements 7.2, 7.5**
/// </summary>
public sealed class DopplerChartCachePropertyTests
{
    /// <summary>
    /// Property 9: Doppler chart cached metrics equal LINQ-computed values.
    /// For any non-empty list of DopplerInsightChartSample values, the cached MaxSeconds,
    /// MaxHz, MaxElev, and TcaIndex SHALL equal the values that would be produced by
    /// the original LINQ expressions over the same sample list.
    ///
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Cached_metrics_equal_linq_computed_values(byte sampleCountByte)
    {
        var sampleCount = (sampleCountByte % 20) + 1; // 1 to 20 samples
        var samples = GenerateSamples(sampleCount);

        // Compute using the static methods (equivalent to original LINQ)
        var expectedMaxSeconds = DopplerTimeSeriesChartControl.ComputeMaxDurationSeconds(samples, []);
        var expectedMaxHz = DopplerTimeSeriesChartControl.ComputeMaxY(samples, []);
        var expectedMaxElev = DopplerTimeSeriesChartControl.ComputeMaxElevation(samples, []);
        var expectedTcaIndex = DopplerTimeSeriesChartControl.ComputeTcaIndex(samples);

        // Verify TCA index is the sample with the highest elevation (same as OrderByDescending.First)
        var linqTca = samples.Select((s, i) => (s, i))
            .OrderByDescending(x => x.s.ElevationDeg)
            .First();

        var tcaCorrect = expectedTcaIndex == linqTca.i;
        var maxSecondsCorrect = Math.Abs(expectedMaxSeconds - Math.Max(1, samples.Max(s => s.SecondsFromStart))) < 0.001;

        // MaxY uses the 1.08 multiplier
        var rawMax = samples.SelectMany(s => new double[]
        {
            s.AbsRxDeltaHz, s.AbsTxDeltaHz, s.EffectiveThresholdHz, s.BaseThresholdHz
        }).Max();
        var expectedMaxHzLinq = rawMax <= 0 ? 0 : rawMax * 1.08;
        var maxHzCorrect = Math.Abs(expectedMaxHz - expectedMaxHzLinq) < 0.001;

        var maxElevCorrect = Math.Abs(expectedMaxElev - samples.Max(s => s.ElevationDeg)) < 0.001;

        return tcaCorrect && maxSecondsCorrect && maxHzCorrect && maxElevCorrect;
    }

    /// <summary>
    /// Property 9 (with comparison samples): Cached metrics consider both primary and comparison.
    ///
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Cached_metrics_consider_both_primary_and_comparison(byte primaryCountByte, byte compareCountByte)
    {
        var primaryCount = (primaryCountByte % 10) + 1;
        var compareCount = (compareCountByte % 10) + 1;
        var primary = GenerateSamples(primaryCount);
        var compare = GenerateSamples(compareCount, seedOffset: 1000);

        var maxSeconds = DopplerTimeSeriesChartControl.ComputeMaxDurationSeconds(primary, compare);
        var maxHz = DopplerTimeSeriesChartControl.ComputeMaxY(primary, compare);
        var maxElev = DopplerTimeSeriesChartControl.ComputeMaxElevation(primary, compare);

        // MaxSeconds should be at least the max from either list (clamped to 1)
        var pMax = primary.Max(s => s.SecondsFromStart);
        var cMax = compare.Max(s => s.SecondsFromStart);
        var expectedMaxSeconds = Math.Max(1, Math.Max(pMax, cMax));
        var maxSecondsCorrect = Math.Abs(maxSeconds - expectedMaxSeconds) < 0.001;

        // MaxElev should be the max from both
        var expectedMaxElev = Math.Max(primary.Max(s => s.ElevationDeg), compare.Max(s => s.ElevationDeg));
        var maxElevCorrect = Math.Abs(maxElev - expectedMaxElev) < 0.001;

        return maxSecondsCorrect && maxElevCorrect;
    }

    /// <summary>
    /// Property 10: Visible-window filter correctness.
    /// For any sample list, zoom level, and pan offset defining a viewport [viewStart, viewEnd],
    /// the filtered list SHALL contain exactly those samples where SecondsFromStart falls within
    /// the viewport bounds, in original order.
    ///
    /// **Validates: Requirements 7.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Visible_window_filter_contains_exactly_in_range_samples(byte sampleCountByte, byte viewStartByte, byte viewEndByte)
    {
        var sampleCount = (sampleCountByte % 30) + 1;
        var samples = GenerateSamples(sampleCount);

        var maxSeconds = samples.Max(s => s.SecondsFromStart);
        var viewStart = (viewStartByte / 255.0) * maxSeconds;
        var viewEnd = viewStart + ((viewEndByte / 255.0) * (maxSeconds - viewStart));
        if (viewEnd < viewStart) viewEnd = viewStart;

        var filtered = DopplerTimeSeriesChartControl.FilterToWindow(samples, viewStart, viewEnd);

        // Verify: every sample in filtered is within bounds
        var allInRange = filtered.All(s =>
            s.SecondsFromStart >= viewStart && s.SecondsFromStart <= viewEnd);

        // Verify: every sample in original that's in bounds is in filtered
        var expectedSamples = samples
            .Where(s => s.SecondsFromStart >= viewStart && s.SecondsFromStart <= viewEnd)
            .ToList();

        var correctCount = filtered.Count == expectedSamples.Count;

        // Verify: order preserved (each element matches by index)
        var orderPreserved = true;
        for (var i = 0; i < filtered.Count; i++)
        {
            if (filtered[i] != expectedSamples[i])
            {
                orderPreserved = false;
                break;
            }
        }

        return allInRange && correctCount && orderPreserved;
    }

    /// <summary>
    /// Property 10: Empty sample list produces empty filtered list.
    ///
    /// **Validates: Requirements 7.5**
    /// </summary>
    [Property(MaxTest = 50)]
    public bool Empty_samples_produce_empty_filtered_list(byte viewStartByte, byte viewEndByte)
    {
        var filtered = DopplerTimeSeriesChartControl.FilterToWindow([], viewStartByte, viewEndByte);
        return filtered.Count == 0;
    }

    /// <summary>
    /// Property 9: TCA index for empty list is -1.
    ///
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Fact]
    public void TcaIndex_for_empty_list_is_negative_one()
    {
        var result = DopplerTimeSeriesChartControl.ComputeTcaIndex([]);
        Assert.Equal(-1, result);
    }

    private static List<DopplerInsightChartSample> GenerateSamples(int count, int seedOffset = 0)
    {
        var rng = new Random(42 + seedOffset);
        var samples = new List<DopplerInsightChartSample>(count);

        for (var i = 0; i < count; i++)
        {
            samples.Add(new DopplerInsightChartSample(
                SecondsFromStart: i * 10.0 + rng.NextDouble() * 5.0,
                AbsRxDeltaHz: rng.Next(0, 5000),
                AbsTxDeltaHz: rng.Next(0, 5000),
                BaseThresholdHz: rng.Next(100, 2000),
                EffectiveThresholdHz: rng.Next(100, 2000),
                ElevationDeg: rng.NextDouble() * 90.0,
                SlewHzPerSec: rng.NextDouble() * 100.0,
                WroteRx: rng.Next(2) == 1,
                WroteTx: rng.Next(2) == 1,
                BelowThreshold: rng.Next(2) == 1));
        }

        return samples;
    }
}
