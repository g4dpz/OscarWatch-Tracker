using System;
using System.Collections.Generic;
using System.Linq;
using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

/// <summary>
/// Core engine for detecting and analyzing conflicts between satellite passes.
/// Implements efficient O(n²) conflict detection optimized for typical pass counts (10-50).
/// </summary>
public sealed class PassConflictEngine : IPassConflictService
{
    private readonly IPassQualityScorer _qualityScorer;
    private static readonly TimeSpan DefaultMinimumOverlap = TimeSpan.FromMinutes(2);

    public PassConflictEngine(IPassQualityScorer qualityScorer)
    {
        _qualityScorer = qualityScorer ?? throw new ArgumentNullException(nameof(qualityScorer));
    }

    /// <inheritdoc />
    public IReadOnlyList<PassConflictInfo> DetectConflicts(
        IReadOnlyList<PassInfo> passes,
        TimeSpan minimumOverlapThreshold = default)
    {
        if (passes.Count < 2) return Array.Empty<PassConflictInfo>();

        var threshold = minimumOverlapThreshold == default 
            ? DefaultMinimumOverlap 
            : minimumOverlapThreshold;

        var conflicts = new List<PassConflictInfo>();

        // O(n²) comparison - optimized for typical pass counts (10-50)
        // Early termination and efficient overlap calculation minimize actual work
        for (int i = 0; i < passes.Count; i++)
        {
            for (int j = i + 1; j < passes.Count; j++)
            {
                var passA = passes[i];
                var passB = passes[j];

                // Quick elimination: if passes are far apart in time, skip detailed calculation
                if (!QuickOverlapCheck(passA, passB))
                    continue;

                var overlap = OverlapPeriod.Calculate(passA, passB);
                if (overlap.HasOverlap && overlap.Duration >= threshold)
                {
                    var conflict = CreateConflictInfo(passA, passB, overlap);
                    conflicts.Add(conflict);
                }
            }
        }

        // Sort by conflict start time for consistent ordering
        return conflicts.OrderBy(c => c.ConflictStartUtc).ToList();
    }

    /// <inheritdoc />
    public PassQualityScore CalculateQualityScore(PassInfo pass, QualityWeights? weights = null)
    {
        return _qualityScorer.CalculateScore(pass, weights);
    }

    /// <inheritdoc />
    public PassInfo? RecommendBestPass(IReadOnlyList<PassInfo> conflictingPasses, QualityWeights? weights = null)
    {
        if (conflictingPasses.Count == 0) return null;
        if (conflictingPasses.Count == 1) return conflictingPasses[0];

        // Calculate quality scores for all passes and return the highest scoring one
        var scoredPasses = conflictingPasses
            .Select(pass => new { Pass = pass, Score = _qualityScorer.CalculateScore(pass, weights) })
            .OrderByDescending(item => item.Score.OverallScore)
            .ToList();

        // Return the highest scoring pass, but only if it's clearly better than others
        var best = scoredPasses[0];
        var secondBest = scoredPasses.Count > 1 ? scoredPasses[1] : null;

        // If the difference is very small, don't make a strong recommendation
        if (secondBest != null && Math.Abs(best.Score.OverallScore - secondBest.Score.OverallScore) < 0.05)
            return null;

        return best.Pass;
    }

    /// <inheritdoc />
    public IReadOnlyList<PassConflictGroup> GroupConflicts(IReadOnlyList<PassConflictInfo> conflicts)
    {
        if (conflicts.Count == 0) return Array.Empty<PassConflictGroup>();

        var groups = new List<PassConflictGroup>();
        var processedConflicts = new HashSet<PassConflictInfo>();

        foreach (var conflict in conflicts)
        {
            if (processedConflicts.Contains(conflict)) continue;

            var group = BuildConflictGroup(conflict, conflicts, processedConflicts);
            groups.Add(group);
        }

        return groups.OrderBy(g => g.ConflictStartUtc).ToList();
    }

    /// <summary>
    /// Quick check to eliminate passes that definitely don't overlap.
    /// This optimization reduces the number of detailed overlap calculations needed.
    /// </summary>
    private static bool QuickOverlapCheck(PassInfo passA, PassInfo passB)
    {
        // No overlap if one pass ends before the other starts
        return passA.AosUtc <= passB.LosUtc && passB.AosUtc <= passA.LosUtc;
    }

    /// <summary>
    /// Creates a PassConflictInfo from two overlapping passes.
    /// </summary>
    private static PassConflictInfo CreateConflictInfo(PassInfo passA, PassInfo passB, OverlapPeriod overlap)
    {
        var severity = overlap.CalculateSeverity(passA, passB);
        var type = overlap.DetermineType(passA, passB);
        var description = BuildConflictDescription(passA, passB, overlap, severity);

        return new PassConflictInfo
        {
            ConflictStartUtc = overlap.Start,
            ConflictEndUtc = overlap.End,
            Duration = overlap.Duration,
            PassA = passA,
            PassB = passB,
            Severity = severity,
            Type = type,
            Description = description
        };
    }

    /// <summary>
    /// Builds a human-readable description of the conflict.
    /// </summary>
    private static string BuildConflictDescription(PassInfo passA, PassInfo passB, OverlapPeriod overlap, ConflictSeverity severity)
    {
        var severityText = severity switch
        {
            ConflictSeverity.Minor => "minor overlap",
            ConflictSeverity.Moderate => "significant overlap", 
            ConflictSeverity.Severe => "major overlap",
            _ => "overlap"
        };

        var durationMinutes = (int)Math.Round(overlap.Duration.TotalMinutes);
        return $"{passA.SatelliteName} and {passB.SatelliteName} have {severityText} for {durationMinutes} minute{(durationMinutes != 1 ? "s" : "")}";
    }

    /// <summary>
    /// Builds a conflict group by finding all conflicts transitively connected to the starting conflict.
    /// </summary>
    private PassConflictGroup BuildConflictGroup(
        PassConflictInfo startingConflict, 
        IReadOnlyList<PassConflictInfo> allConflicts,
        HashSet<PassConflictInfo> processedConflicts)
    {
        var groupConflicts = new List<PassConflictInfo> { startingConflict };
        var groupPasses = new HashSet<string> { startingConflict.PassA.NoradId, startingConflict.PassB.NoradId };
        var toProcess = new Queue<PassConflictInfo>();
        
        toProcess.Enqueue(startingConflict);
        processedConflicts.Add(startingConflict);

        // Find all conflicts transitively connected (sharing satellites)
        while (toProcess.Count > 0)
        {
            var current = toProcess.Dequeue();
            
            foreach (var conflict in allConflicts)
            {
                if (processedConflicts.Contains(conflict)) continue;
                
                // Check if this conflict shares a satellite with our group
                var sharesPassA = groupPasses.Contains(conflict.PassA.NoradId);
                var sharesPassB = groupPasses.Contains(conflict.PassB.NoradId);
                
                if (sharesPassA || sharesPassB)
                {
                    groupConflicts.Add(conflict);
                    groupPasses.Add(conflict.PassA.NoradId);
                    groupPasses.Add(conflict.PassB.NoradId);
                    toProcess.Enqueue(conflict);
                    processedConflicts.Add(conflict);
                }
            }
        }

        // Build the final group
        var passes = groupConflicts
            .SelectMany(c => new[] { c.PassA, c.PassB })
            .GroupBy(p => p.NoradId)
            .Select(g => g.First())
            .OrderBy(p => p.AosUtc)
            .ToList();

        var conflictStart = passes.Min(p => p.AosUtc);
        var conflictEnd = passes.Max(p => p.LosUtc);
        var maxSeverity = groupConflicts.Max(c => c.Severity);

        return new PassConflictGroup
        {
            Passes = passes,
            Conflicts = groupConflicts,
            ConflictStartUtc = conflictStart,
            ConflictEndUtc = conflictEnd,
            MaxSeverity = maxSeverity
        };
    }
}