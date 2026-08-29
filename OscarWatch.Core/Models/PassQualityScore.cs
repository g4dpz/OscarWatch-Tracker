using System;

namespace OscarWatch.Core.Models;

/// <summary>
/// Represents a multi-dimensional quality score for a satellite pass.
/// </summary>
public sealed class PassQualityScore
{
    /// <summary>
    /// Elevation-based score (0-1), with higher passes scoring better.
    /// Uses logarithmic scaling to emphasize high-elevation advantages.
    /// </summary>
    public double ElevationScore { get; init; }
    
    /// <summary>
    /// Duration-based score (0-1), with optimal pass lengths scoring highest.
    /// Accounts for diminishing returns on very long passes.
    /// </summary>
    public double DurationScore { get; init; }
    
    /// <summary>
    /// Mode complexity score (0-1), with easier modes scoring higher.
    /// FM > Linear > Digital in typical operator preference.
    /// </summary>
    public double ModeScore { get; init; }
    
    /// <summary>
    /// Popularity/activity score (0-1) based on community usage data.
    /// More active satellites score higher for contact probability.
    /// </summary>
    public double PopularityScore { get; init; }
    
    /// <summary>
    /// Overall weighted score combining all factors (0-1).
    /// </summary>
    public double OverallScore { get; init; }
    
    /// <summary>
    /// Human-readable explanation of why this pass received its score.
    /// Includes the key factors that influenced the rating.
    /// </summary>
    public string Explanation { get; init; } = "";
    
    /// <summary>
    /// Formats the overall score as a star rating (1-5 stars).
    /// </summary>
    public string StarRating
    {
        get
        {
            var stars = (int)Math.Round(OverallScore * 5);
            return new string('⭐', Math.Max(1, Math.Min(5, stars)));
        }
    }
}

/// <summary>
/// Configurable weights for different aspects of pass quality scoring.
/// </summary>
public sealed class QualityWeights
{
    /// <summary>
    /// Weight for elevation in overall score calculation.
    /// Default: 0.4 (40% of total score).
    /// </summary>
    public double Elevation { get; set; } = 0.4;
    
    /// <summary>
    /// Weight for pass duration in overall score calculation.
    /// Default: 0.3 (30% of total score).
    /// </summary>
    public double Duration { get; set; } = 0.3;
    
    /// <summary>
    /// Weight for satellite mode complexity in overall score calculation.
    /// Default: 0.2 (20% of total score).
    /// </summary>
    public double Mode { get; set; } = 0.2;
    
    /// <summary>
    /// Weight for satellite popularity/activity in overall score calculation.
    /// Default: 0.1 (10% of total score).
    /// </summary>
    public double Popularity { get; set; } = 0.1;
    
    /// <summary>
    /// Validates that all weights sum to approximately 1.0.
    /// </summary>
    public bool IsValid => Math.Abs((Elevation + Duration + Mode + Popularity) - 1.0) < 0.01;
    
    /// <summary>
    /// Normalizes weights to sum to 1.0 while preserving relative proportions.
    /// </summary>
    public QualityWeights Normalize()
    {
        var total = Elevation + Duration + Mode + Popularity;
        if (total == 0) return new QualityWeights();
        
        return new QualityWeights
        {
            Elevation = Elevation / total,
            Duration = Duration / total,
            Mode = Mode / total,
            Popularity = Popularity / total
        };
    }
}