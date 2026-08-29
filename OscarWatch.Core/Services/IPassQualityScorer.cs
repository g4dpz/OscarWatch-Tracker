using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

/// <summary>
/// Service for calculating quality scores for satellite passes based on multiple factors.
/// </summary>
public interface IPassQualityScorer
{
    /// <summary>
    /// Calculates a comprehensive quality score for a satellite pass.
    /// </summary>
    /// <param name="pass">The satellite pass to evaluate</param>
    /// <param name="weights">Optional custom weights for scoring factors</param>
    /// <returns>Detailed quality score with component breakdown and explanation</returns>
    PassQualityScore CalculateScore(PassInfo pass, QualityWeights? weights = null);
}