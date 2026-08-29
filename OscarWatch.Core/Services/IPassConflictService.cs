using System;
using System.Collections.Generic;
using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

/// <summary>
/// Service for detecting and analyzing conflicts between satellite passes.
/// </summary>
public interface IPassConflictService
{
    /// <summary>
    /// Detects conflicts among the provided satellite passes.
    /// </summary>
    /// <param name="passes">List of satellite passes to analyze for conflicts</param>
    /// <param name="minimumOverlapThreshold">Minimum overlap duration to consider a conflict (default: 2 minutes)</param>
    /// <returns>List of detected conflicts, sorted by conflict start time</returns>
    IReadOnlyList<PassConflictInfo> DetectConflicts(
        IReadOnlyList<PassInfo> passes,
        TimeSpan minimumOverlapThreshold = default);

    /// <summary>
    /// Calculates a quality score for a satellite pass based on multiple factors.
    /// </summary>
    /// <param name="pass">The satellite pass to score</param>
    /// <param name="weights">Optional custom weights for scoring factors</param>
    /// <returns>Detailed quality score with explanation</returns>
    PassQualityScore CalculateQualityScore(PassInfo pass, QualityWeights? weights = null);

    /// <summary>
    /// Recommends the best pass from a group of conflicting passes.
    /// </summary>
    /// <param name="conflictingPasses">List of passes that conflict with each other</param>
    /// <param name="weights">Optional custom weights for quality scoring</param>
    /// <returns>The recommended pass, or null if no clear recommendation can be made</returns>
    PassInfo? RecommendBestPass(IReadOnlyList<PassInfo> conflictingPasses, QualityWeights? weights = null);

    /// <summary>
    /// Groups individual conflicts into conflict sets for multi-way conflicts.
    /// </summary>
    /// <param name="conflicts">List of pairwise conflicts</param>
    /// <returns>List of conflict groups, each containing all mutually conflicting passes</returns>
    IReadOnlyList<PassConflictGroup> GroupConflicts(IReadOnlyList<PassConflictInfo> conflicts);
}

/// <summary>
/// Represents a group of satellite passes that all conflict with each other.
/// </summary>
public sealed class PassConflictGroup
{
    /// <summary>
    /// All satellite passes that participate in this conflict.
    /// </summary>
    public required IReadOnlyList<PassInfo> Passes { get; init; }
    
    /// <summary>
    /// Individual pairwise conflicts within this group.
    /// </summary>
    public required IReadOnlyList<PassConflictInfo> Conflicts { get; init; }
    
    /// <summary>
    /// Overall start time of the conflict (earliest AOS of all passes).
    /// </summary>
    public DateTime ConflictStartUtc { get; init; }
    
    /// <summary>
    /// Overall end time of the conflict (latest LOS of all passes).
    /// </summary>
    public DateTime ConflictEndUtc { get; init; }
    
    /// <summary>
    /// Total duration spanning all conflicting passes.
    /// </summary>
    public TimeSpan TotalDuration => ConflictEndUtc - ConflictStartUtc;
    
    /// <summary>
    /// The most severe conflict within this group.
    /// </summary>
    public ConflictSeverity MaxSeverity { get; init; }
    
    /// <summary>
    /// Number of satellites involved in this conflict.
    /// </summary>
    public int SatelliteCount => Passes.Count;
    
    /// <summary>
    /// True if this is a multi-way conflict (3+ satellites).
    /// </summary>
    public bool IsMultiWay => SatelliteCount >= 3;
}