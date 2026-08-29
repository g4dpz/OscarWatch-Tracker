using System;

namespace OscarWatch.Core.Models;

/// <summary>
/// Settings for pass conflict detection and quality scoring preferences.
/// </summary>
public sealed class ConflictDetectionSettings
{
    public const int DefaultMinimumOverlapMinutes = 2;
    public const int MinMinimumOverlapMinutes = 1;
    public const int MaxMinimumOverlapMinutes = 15;
    
    public const double DefaultElevationWeight = 0.40;
    public const double DefaultDurationWeight = 0.30;
    public const double DefaultModeWeight = 0.20;
    public const double DefaultPopularityWeight = 0.10;
    
    public const double MinWeight = 0.0;
    public const double MaxWeight = 1.0;

    /// <summary>
    /// Whether pass conflict detection is enabled.
    /// </summary>
    public bool ConflictDetectionEnabled { get; set; } = true;

    /// <summary>
    /// Whether quality scoring is enabled and displayed.
    /// </summary>
    public bool QualityScoreEnabled { get; set; } = true;

    /// <summary>
    /// Minimum overlap duration in minutes to consider a conflict (1-15 minutes).
    /// </summary>
    public int MinimumOverlapMinutes { get; set; } = DefaultMinimumOverlapMinutes;

    /// <summary>
    /// Weight factor for elevation in quality scoring (0.0-1.0).
    /// Higher elevation passes get better scores.
    /// </summary>
    public double ElevationWeight { get; set; } = DefaultElevationWeight;

    /// <summary>
    /// Weight factor for pass duration in quality scoring (0.0-1.0).
    /// Passes closer to optimal duration (8-12 minutes) get better scores.
    /// </summary>
    public double DurationWeight { get; set; } = DefaultDurationWeight;

    /// <summary>
    /// Weight factor for transponder mode complexity in quality scoring (0.0-1.0).
    /// More complex modes (SSB, CW) get better scores than simpler ones (FM).
    /// </summary>
    public double ModeWeight { get; set; } = DefaultModeWeight;

    /// <summary>
    /// Weight factor for satellite popularity in quality scoring (0.0-1.0).
    /// More popular/active satellites get better scores.
    /// </summary>
    public double PopularityWeight { get; set; } = DefaultPopularityWeight;

    /// <summary>
    /// Gets the quality weights as a structured object for the scoring service.
    /// </summary>
    public QualityWeights GetQualityWeights() => new()
    {
        Elevation = ElevationWeight,
        Duration = DurationWeight,
        Mode = ModeWeight,
        Popularity = PopularityWeight
    };

    /// <summary>
    /// Sets the quality weights from a structured object.
    /// </summary>
    public void SetQualityWeights(QualityWeights weights)
    {
        ElevationWeight = ClampWeight(weights.Elevation);
        DurationWeight = ClampWeight(weights.Duration);
        ModeWeight = ClampWeight(weights.Mode);
        PopularityWeight = ClampWeight(weights.Popularity);
    }

    /// <summary>
    /// Clamps the minimum overlap minutes to valid range.
    /// </summary>
    public static int ClampMinimumOverlapMinutes(int minutes) =>
        Math.Clamp(minutes, MinMinimumOverlapMinutes, MaxMinimumOverlapMinutes);

    /// <summary>
    /// Clamps a weight value to valid range (0.0-1.0).
    /// </summary>
    public static double ClampWeight(double weight) =>
        Math.Clamp(weight, MinWeight, MaxWeight);

    /// <summary>
    /// Validates and normalizes the weight values to ensure they sum appropriately.
    /// If weights don't sum to 1.0, they are proportionally adjusted.
    /// </summary>
    public void NormalizeWeights()
    {
        // Clamp all weights to valid ranges first
        ElevationWeight = ClampWeight(ElevationWeight);
        DurationWeight = ClampWeight(DurationWeight);
        ModeWeight = ClampWeight(ModeWeight);
        PopularityWeight = ClampWeight(PopularityWeight);

        // Calculate total weight
        var totalWeight = ElevationWeight + DurationWeight + ModeWeight + PopularityWeight;

        // If total is close to zero, reset to defaults
        if (totalWeight < 0.01)
        {
            ElevationWeight = DefaultElevationWeight;
            DurationWeight = DefaultDurationWeight;
            ModeWeight = DefaultModeWeight;
            PopularityWeight = DefaultPopularityWeight;
            return;
        }

        // If total is not 1.0, normalize proportionally
        if (Math.Abs(totalWeight - 1.0) > 0.01)
        {
            ElevationWeight /= totalWeight;
            DurationWeight /= totalWeight;
            ModeWeight /= totalWeight;
            PopularityWeight /= totalWeight;
        }
    }

    /// <summary>
    /// Gets the minimum overlap threshold as a TimeSpan for use with the conflict service.
    /// </summary>
    public TimeSpan GetMinimumOverlapThreshold() => TimeSpan.FromMinutes(MinimumOverlapMinutes);
}