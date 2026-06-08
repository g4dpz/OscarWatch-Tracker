// Feature: performance-optimisations, Property 1: Label ordering equivalence

using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Controls;
using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 1.1, 1.2, 1.3**
///
/// Property-based tests verifying that <see cref="LabelOrderBuffer"/> produces the same
/// index sequence as the LINQ-based reference implementation for all inputs.
/// </summary>
public class LabelOrderBufferPropertyTests
{
    /// <summary>
    /// Reference implementation using LINQ (the original code path).
    /// </summary>
    private static List<int> ReferenceOrder(
        IReadOnlyList<SatelliteTrackState> states,
        string? focusedNoradId,
        bool soloFocused)
    {
        return Enumerable.Range(0, states.Count)
            .Where(i => TrackingPlotAccessibility.IsPlotSatelliteVisible(soloFocused, focusedNoradId, states[i].NoradId))
            .OrderBy(i => states[i].NoradId == focusedNoradId ? 1 : 0)
            .ToList();
    }

    /// <summary>
    /// Create a minimal SatelliteTrackState with just a NoradId set.
    /// Other required fields use dummy values.
    /// </summary>
    private static SatelliteTrackState MakeState(string noradId) => new()
    {
        Name = noradId,
        NoradId = noradId,
        Subpoint = new GeoCoordinate(0, 0, 400)
    };

    /// <summary>
    /// Property 1: Label ordering equivalence.
    ///
    /// For any list of 0–30 SatelliteTrackState entries with unique NoradIds,
    /// any focused NORAD ID (null, matching, or absent), and any soloFocused flag,
    /// the LabelOrderBuffer SHALL produce the same index sequence as the LINQ-based
    /// reference implementation.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Label_order_matches_reference(byte stateCountByte, byte[] noradIdSeeds, bool soloFocused, byte focusedStrategy)
    {
        // Map state count to 0–30 range
        var stateCount = stateCountByte % 31;

        // Build states with unique NoradIds (real satellites always have unique catalogue numbers)
        var states = new List<SatelliteTrackState>(stateCount);
        for (var i = 0; i < stateCount; i++)
        {
            var seed = (noradIdSeeds is not null && i < noradIdSeeds.Length)
                ? noradIdSeeds[i]
                : (byte)i;
            // Unique NoradId per state using combination of seed and index
            var noradId = $"{seed:D3}{i:D3}";
            states.Add(MakeState(noradId));
        }

        // Determine focused NORAD ID based on strategy
        var strategy = focusedStrategy % 4;
        string? focusedNoradId = strategy switch
        {
            0 => null,                                          // null
            1 => "",                                            // empty
            2 when states.Count > 0 => states[focusedStrategy % states.Count].NoradId, // matching
            _ => "99999"                                        // absent from states
        };

        // Build using LabelOrderBuffer
        var buffer = new LabelOrderBuffer(Math.Max(64, stateCount + 1));
        buffer.Build(states, focusedNoradId, soloFocused);
        var bufferResult = buffer.Indices.ToArray();

        // Build using LINQ reference
        var referenceResult = ReferenceOrder(states, focusedNoradId, soloFocused);

        // Assert sequences are identical
        if (bufferResult.Length != referenceResult.Count)
            return false;

        for (var i = 0; i < bufferResult.Length; i++)
        {
            if (bufferResult[i] != referenceResult[i])
                return false;
        }

        return true;
    }
}
