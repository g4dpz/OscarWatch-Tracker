using System;
using System.Text;
using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

/// <summary>
/// Implementation of pass quality scoring based on elevation, duration, mode complexity, and popularity.
/// </summary>
public sealed class PassQualityScorer : IPassQualityScorer
{
    private static readonly QualityWeights DefaultWeights = new();

    /// <inheritdoc />
    public PassQualityScore CalculateScore(PassInfo pass, QualityWeights? weights = null)
    {
        weights ??= DefaultWeights;
        
        // Ensure weights are valid and normalized
        if (!weights.IsValid)
            weights = weights.Normalize();

        var elevationScore = CalculateElevationScore(pass.MaxElevationDeg);
        var durationScore = CalculateDurationScore(pass.Duration);
        var modeScore = CalculateModeScore(pass.NoradId);
        var popularityScore = CalculatePopularityScore(pass.NoradId);

        var overallScore = 
            elevationScore * weights.Elevation +
            durationScore * weights.Duration +
            modeScore * weights.Mode +
            popularityScore * weights.Popularity;

        var explanation = BuildExplanation(pass, elevationScore, durationScore, modeScore, popularityScore);

        return new PassQualityScore
        {
            ElevationScore = elevationScore,
            DurationScore = durationScore,
            ModeScore = modeScore,
            PopularityScore = popularityScore,
            OverallScore = Math.Max(0, Math.Min(1, overallScore)),
            Explanation = explanation
        };
    }

    /// <summary>
    /// Calculates elevation-based score using logarithmic scaling to emphasize high passes.
    /// </summary>
    private static double CalculateElevationScore(double elevationDeg)
    {
        if (elevationDeg <= 0) return 0;
        if (elevationDeg >= 90) return 1.0;

        // Logarithmic scaling - higher passes disproportionately better
        // Base score from linear elevation, with logarithmic bonus for high passes
        var linearScore = elevationDeg / 90.0;
        var bonusMultiplier = 1.0 + Math.Log10(elevationDeg / 10.0 + 1) * 0.3;
        
        return Math.Min(1.0, linearScore * bonusMultiplier);
    }

    /// <summary>
    /// Calculates duration-based score with optimal range around 8-12 minutes.
    /// </summary>
    private static double CalculateDurationScore(TimeSpan duration)
    {
        var minutes = duration.TotalMinutes;
        
        // Very short passes are poor quality
        if (minutes < 2) return 0.1;
        
        // Optimal range: 8-12 minutes gets full score
        if (minutes >= 8 && minutes <= 12) return 1.0;
        
        // Gradual falloff outside optimal range
        if (minutes < 8)
        {
            // Ramp up from 2-8 minutes
            return 0.1 + (minutes - 2) / 6.0 * 0.9;
        }
        
        // Diminishing returns for very long passes
        if (minutes > 20) return 0.6;
        
        // Gradual decline from 12-20 minutes
        return 1.0 - (minutes - 12) / 8.0 * 0.4;
    }

    /// <summary>
    /// Calculates mode complexity score based on satellite type.
    /// FM satellites are easier for beginners, linear transponders more complex.
    /// </summary>
    private static double CalculateModeScore(string noradId)
    {
        // This is a simplified implementation - in a full system, this would
        // look up actual transponder data from the satellite database
        
        // For now, use NORAD ID patterns to make educated guesses
        // Common FM satellites: SO-50, AO-91, AO-92, ISS, etc.
        var knownFmSatellites = new[]
        {
            "27607", // SO-50
            "43017", // AO-91  
            "43137", // AO-92
            "25544"  // ISS
        };

        foreach (var fmSat in knownFmSatellites)
        {
            if (string.Equals(noradId, fmSat, StringComparison.Ordinal))
                return 1.0; // FM satellites score highest for ease of use
        }

        // Linear transponder satellites (more complex)
        var knownLinearSatellites = new[]
        {
            "07530", // AO-7
            "24278", // FO-29
            "44909"  // RS-44
        };

        foreach (var linearSat in knownLinearSatellites)
        {
            if (string.Equals(noradId, linearSat, StringComparison.Ordinal))
                return 0.7; // Linear transponders moderately complex
        }

        // Digital/experimental satellites (most complex)
        // Default assumption for unknown satellites
        return 0.5; // Neutral score for unknown modes
    }

    /// <summary>
    /// Calculates popularity score based on community activity levels.
    /// More active satellites have higher probability of successful contacts.
    /// </summary>
    private static double CalculatePopularityScore(string noradId)
    {
        // This would ideally integrate with community status data
        // For now, use common knowledge about popular satellites
        
        var highPopularitySatellites = new[]
        {
            "27607", // SO-50 - very popular FM satellite
            "43017", // AO-91 - popular FM satellite  
            "25544"  // ISS - extremely popular for SSTV and voice
        };

        foreach (var popularSat in highPopularitySatellites)
        {
            if (string.Equals(noradId, popularSat, StringComparison.Ordinal))
                return 1.0;
        }

        var moderatePopularitySatellites = new[]
        {
            "43137", // AO-92
            "44909", // RS-44
            "07530"  // AO-7
        };

        foreach (var moderateSat in moderatePopularitySatellites)
        {
            if (string.Equals(noradId, moderateSat, StringComparison.Ordinal))
                return 0.7;
        }

        // Default moderate popularity for unknown satellites
        return 0.5;
    }

    /// <summary>
    /// Builds a human-readable explanation of the quality score.
    /// </summary>
    private static string BuildExplanation(
        PassInfo pass, 
        double elevationScore, 
        double durationScore, 
        double modeScore, 
        double popularityScore)
    {
        var explanation = new StringBuilder();
        
        // Elevation explanation
        if (elevationScore >= 0.8)
            explanation.Append($"Excellent elevation ({pass.MaxElevationDeg:F0}°)");
        else if (elevationScore >= 0.6)
            explanation.Append($"Good elevation ({pass.MaxElevationDeg:F0}°)");
        else if (elevationScore >= 0.4)
            explanation.Append($"Moderate elevation ({pass.MaxElevationDeg:F0}°)");
        else
            explanation.Append($"Low elevation ({pass.MaxElevationDeg:F0}°)");

        // Duration explanation
        var durationMinutes = (int)Math.Round(pass.Duration.TotalMinutes);
        if (durationScore >= 0.8)
            explanation.Append($", optimal duration ({durationMinutes} min)");
        else if (durationScore >= 0.6)
            explanation.Append($", good duration ({durationMinutes} min)");
        else if (durationScore >= 0.4)
            explanation.Append($", adequate duration ({durationMinutes} min)");
        else
            explanation.Append($", short duration ({durationMinutes} min)");

        // Mode explanation (simplified)
        if (modeScore >= 0.9)
            explanation.Append(", easy FM mode");
        else if (modeScore >= 0.6)
            explanation.Append(", linear transponder");
        else
            explanation.Append(", digital/complex mode");

        // Popularity explanation
        if (popularityScore >= 0.9)
            explanation.Append(", very active satellite");
        else if (popularityScore >= 0.6)
            explanation.Append(", moderately active");
        else
            explanation.Append(", less active satellite");

        return explanation.ToString();
    }
}