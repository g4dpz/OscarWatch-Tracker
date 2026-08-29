using System;

namespace OscarWatch.Core.Models;

/// <summary>
/// Represents a time period where two satellite passes overlap.
/// </summary>
internal readonly struct OverlapPeriod
{
    /// <summary>
    /// Start time of the overlap (latest AOS of the two passes).
    /// </summary>
    public DateTime Start { get; init; }
    
    /// <summary>
    /// End time of the overlap (earliest LOS of the two passes).
    /// </summary>
    public DateTime End { get; init; }
    
    /// <summary>
    /// Duration of the overlap period.
    /// </summary>
    public TimeSpan Duration { get; init; }
    
    /// <summary>
    /// True if there is any overlap between the passes.
    /// </summary>
    public bool HasOverlap => Duration > TimeSpan.Zero;
    
    /// <summary>
    /// Returns an OverlapPeriod representing no overlap.
    /// </summary>
    public static OverlapPeriod None => new() { Duration = TimeSpan.Zero };
    
    /// <summary>
    /// Creates an OverlapPeriod from two satellite passes.
    /// </summary>
    public static OverlapPeriod Calculate(PassInfo passA, PassInfo passB)
    {
        var start = passA.AosUtc > passB.AosUtc ? passA.AosUtc : passB.AosUtc;
        var end = passA.LosUtc < passB.LosUtc ? passA.LosUtc : passB.LosUtc;
        
        return start < end 
            ? new OverlapPeriod { Start = start, End = end, Duration = end - start }
            : None;
    }
    
    /// <summary>
    /// Calculates the severity of conflict based on overlap characteristics.
    /// </summary>
    public ConflictSeverity CalculateSeverity(PassInfo passA, PassInfo passB)
    {
        if (!HasOverlap) return ConflictSeverity.Minor;
        
        var shorterPassDuration = passA.Duration < passB.Duration ? passA.Duration : passB.Duration;
        var overlapPercentage = Duration.TotalMilliseconds / shorterPassDuration.TotalMilliseconds;
        
        return overlapPercentage switch
        {
            >= 0.75 => ConflictSeverity.Severe,
            >= 0.25 => ConflictSeverity.Moderate,
            _ => ConflictSeverity.Minor
        };
    }
    
    /// <summary>
    /// Determines the type of conflict based on timing relationships.
    /// </summary>
    public PassConflictType DetermineType(PassInfo passA, PassInfo passB)
    {
        if (!HasOverlap) return PassConflictType.PartialOverlap;
        
        // Check for full containment (one pass completely within another)
        var aContainsB = passA.AosUtc <= passB.AosUtc && passA.LosUtc >= passB.LosUtc;
        var bContainsA = passB.AosUtc <= passA.AosUtc && passB.LosUtc >= passA.LosUtc;
        
        if (aContainsB || bContainsA)
            return PassConflictType.FullContainment;
        
        // Default to partial overlap
        return PassConflictType.PartialOverlap;
    }
}