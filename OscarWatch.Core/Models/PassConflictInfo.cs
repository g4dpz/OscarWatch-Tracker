using System;

namespace OscarWatch.Core.Models;

/// <summary>
/// Represents a conflict between two satellite passes that overlap in time.
/// </summary>
public sealed class PassConflictInfo
{
    /// <summary>
    /// UTC time when the conflict begins (later of the two AOS times).
    /// </summary>
    public DateTime ConflictStartUtc { get; init; }
    
    /// <summary>
    /// UTC time when the conflict ends (earlier of the two LOS times).
    /// </summary>
    public DateTime ConflictEndUtc { get; init; }
    
    /// <summary>
    /// Duration of the overlapping period.
    /// </summary>
    public TimeSpan Duration { get; init; }
    
    /// <summary>
    /// First satellite pass in the conflict.
    /// </summary>
    public required PassInfo PassA { get; init; }
    
    /// <summary>
    /// Second satellite pass in the conflict.
    /// </summary>
    public required PassInfo PassB { get; init; }
    
    /// <summary>
    /// Severity level of the conflict based on overlap percentage.
    /// </summary>
    public ConflictSeverity Severity { get; init; }
    
    /// <summary>
    /// Type of conflict (partial overlap, full containment, etc.).
    /// </summary>
    public PassConflictType Type { get; init; }
    
    /// <summary>
    /// Human-readable description of the conflict.
    /// </summary>
    public string Description { get; init; } = "";
}

/// <summary>
/// Severity levels for pass conflicts based on overlap characteristics.
/// </summary>
public enum ConflictSeverity
{
    /// <summary>
    /// Minor overlap (&lt; 25% of shorter pass duration).
    /// Easy to switch between passes or choose optimal portion.
    /// </summary>
    Minor,
    
    /// <summary>
    /// Moderate overlap (25-75% of shorter pass duration).
    /// Requires decision about which pass to prioritize.
    /// </summary>
    Moderate,
    
    /// <summary>
    /// Severe overlap (&gt; 75% of shorter pass duration).
    /// Passes are essentially mutually exclusive - must choose one.
    /// </summary>
    Severe
}

/// <summary>
/// Types of pass conflicts based on timing relationships.
/// </summary>
public enum PassConflictType
{
    /// <summary>
    /// Passes partially overlap - some time available for each.
    /// </summary>
    PartialOverlap,
    
    /// <summary>
    /// One pass is completely contained within another.
    /// </summary>
    FullContainment,
    
    /// <summary>
    /// Passes are adjacent with insufficient time to switch between them.
    /// </summary>
    Adjacent,
    
    /// <summary>
    /// Part of a multi-way conflict involving 3 or more satellites.
    /// </summary>
    MultiWay
}