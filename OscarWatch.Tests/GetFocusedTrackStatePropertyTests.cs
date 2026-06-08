// Feature: performance-optimisations, Property 7: GetFocusedTrackState for-loop equivalence

using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 7.1, 7.2**
///
/// Property-based tests verifying that the for-loop implementation of GetFocusedTrackState
/// returns the same result as the LINQ FirstOrDefault reference for all inputs.
/// </summary>
public class GetFocusedTrackStatePropertyTests
{
    /// <summary>
    /// Reference implementation using LINQ FirstOrDefault (the original code path).
    /// </summary>
    private static SatelliteTrackState? ReferenceGetFocusedTrackState(
        IReadOnlyList<SatelliteTrackState> states, string? focusedNoradId)
    {
        if (string.IsNullOrEmpty(focusedNoradId))
            return null;

        return states.FirstOrDefault(s => string.Equals(s.NoradId, focusedNoradId, StringComparison.Ordinal));
    }

    /// <summary>
    /// For-loop implementation (replicates the private static method in MainViewModel).
    /// </summary>
    private static SatelliteTrackState? ForLoopGetFocusedTrackState(
        IReadOnlyList<SatelliteTrackState> states, string? focusedNoradId)
    {
        if (string.IsNullOrEmpty(focusedNoradId))
            return null;

        for (var i = 0; i < states.Count; i++)
        {
            if (string.Equals(states[i].NoradId, focusedNoradId, StringComparison.Ordinal))
                return states[i];
        }

        return null;
    }

    /// <summary>
    /// Create a minimal SatelliteTrackState with a given NoradId.
    /// </summary>
    private static SatelliteTrackState MakeState(string noradId) => new()
    {
        Name = noradId,
        NoradId = noradId,
        Subpoint = new GeoCoordinate(0, 0, 400)
    };

    /// <summary>
    /// Property 7: GetFocusedTrackState for-loop equivalence.
    ///
    /// For any list of 0–50 SatelliteTrackState entries and any focusedNoradId
    /// (including null, empty, present in the list, and absent from the list),
    /// the for-loop implementation SHALL return the same result as FirstOrDefault LINQ reference.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool ForLoop_matches_FirstOrDefault_reference(byte stateCountByte, byte[] noradIdSeeds, byte focusedStrategy)
    {
        // Map state count to 0–50 range
        var stateCount = stateCountByte % 51;

        // Build states with NoradIds (allowing duplicates to exercise first-match semantics)
        var states = new List<SatelliteTrackState>(stateCount);
        for (var i = 0; i < stateCount; i++)
        {
            var seed = (noradIdSeeds is not null && i < noradIdSeeds.Length)
                ? noradIdSeeds[i]
                : (byte)i;
            var noradId = $"{seed:D3}{i % 10:D1}";
            states.Add(MakeState(noradId));
        }

        // Determine focused NORAD ID based on strategy (covers null, empty, present, absent)
        var strategy = focusedStrategy % 5;
        string? focusedNoradId = strategy switch
        {
            0 => null,                                                              // null
            1 => "",                                                                // empty
            2 when states.Count > 0 => states[focusedStrategy % states.Count].NoradId, // present in list
            3 => "ABSENT_ID_99999",                                                 // absent from list
            _ => "ABSENT_ID_88888"                                                  // also absent (covers strategy 2 with empty list and strategy 4)
        };

        // Execute both implementations
        var expected = ReferenceGetFocusedTrackState(states, focusedNoradId);
        var actual = ForLoopGetFocusedTrackState(states, focusedNoradId);

        // Both should return the same result (same reference or both null)
        return ReferenceEquals(expected, actual);
    }
}
