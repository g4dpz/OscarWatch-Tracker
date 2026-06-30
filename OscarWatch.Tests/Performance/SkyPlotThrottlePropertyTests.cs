// Feature: startup-io-rendering-optimisation, Property 6: SkyPlot render threshold

using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Controls;
using OscarWatch.Core.Models;

namespace OscarWatch.Tests.Performance;

/// <summary>
/// Property 6: For any set of satellite screen-space positions and a previous-position cache,
/// <c>HasMovedBeyondThreshold</c> SHALL return true if and only if at least one satellite has
/// moved ≥ 1 pixel (Euclidean distance) from its cached position.
///
/// **Validates: Requirements 4.2, 4.3**
/// </summary>
public class SkyPlotThrottlePropertyTests
{
    private const double Cx = 200.0;
    private const double Cy = 200.0;
    private const double PlotRadius = 180.0;

    /// <summary>
    /// Helper: creates a SatelliteTrackState with a given azimuth and elevation.
    /// </summary>
    private static SatelliteTrackState MakeState(string noradId, double azDeg, double elDeg) => new()
    {
        Name = $"SAT-{noradId}",
        NoradId = noradId,
        Subpoint = new GeoCoordinate(0, 0),
        LookAngles = new LookAngles(azDeg, elDeg, 400.0)
    };

    /// <summary>
    /// Returns true when at least one satellite moves >= 1px from its cached position.
    /// We generate random "old" and "new" az/el pairs, compute their screen positions,
    /// then assert the method agrees with a manual distance check.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Returns_true_iff_any_satellite_moved_at_least_1px(PositiveInt seedRaw)
    {
        var rng = new Random(seedRaw.Get);

        // Generate 1-5 satellites with random positions
        var count = rng.Next(1, 6);
        var oldStates = new List<SatelliteTrackState>();
        var newStates = new List<SatelliteTrackState>();
        var previousPositions = new Dictionary<string, (double X, double Y)>();

        for (var i = 0; i < count; i++)
        {
            var id = $"SAT{i:D5}";

            // Old position (random az 0-360, el 0-90)
            var oldAz = rng.NextDouble() * 360.0;
            var oldEl = rng.NextDouble() * 90.0;
            oldStates.Add(MakeState(id, oldAz, oldEl));

            // Compute old screen position and cache it
            SkyPlotControl.TryAzElToPoint(Cx, Cy, PlotRadius, oldAz, oldEl, out var oldPoint);
            previousPositions[id] = oldPoint;

            // New position (slight perturbation or large change)
            var newAz = oldAz + (rng.NextDouble() - 0.5) * 10.0; // +/- 5 degrees
            var newEl = Math.Clamp(oldEl + (rng.NextDouble() - 0.5) * 5.0, 0, 90); // +/- 2.5 degrees
            newStates.Add(MakeState(id, newAz, newEl));
        }

        // Compute expected result manually
        var expectedShouldRender = false;
        foreach (var state in newStates)
        {
            var la = state.LookAngles!;
            SkyPlotControl.TryAzElToPoint(Cx, Cy, PlotRadius, la.AzimuthDeg, la.ElevationDeg, out var newPoint);
            var prev = previousPositions[state.NoradId];
            var dx = newPoint.X - prev.X;
            var dy = newPoint.Y - prev.Y;
            if (dx * dx + dy * dy >= 1.0)
            {
                expectedShouldRender = true;
                break;
            }
        }

        // Test via the internal static method
        var actual = SkyPlotControl.HasMovedBeyondThreshold(
            newStates, previousPositions, Cx, Cy, PlotRadius);

        return actual == expectedShouldRender;
    }

    /// <summary>
    /// When a new satellite appears (not in previous cache), always returns true.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Returns_true_when_new_satellite_appears(PositiveInt seedRaw)
    {
        var rng = new Random(seedRaw.Get);

        // Create a cache with one existing satellite
        var previousPositions = new Dictionary<string, (double X, double Y)>();
        var oldAz = rng.NextDouble() * 360.0;
        var oldEl = rng.NextDouble() * 90.0;
        SkyPlotControl.TryAzElToPoint(Cx, Cy, PlotRadius, oldAz, oldEl, out var oldPoint);
        previousPositions["SAT00001"] = oldPoint;

        // New states include the old satellite (unmoved) plus a new one
        var states = new List<SatelliteTrackState>
        {
            MakeState("SAT00001", oldAz, oldEl),
            MakeState("SAT00002", rng.NextDouble() * 360.0, rng.NextDouble() * 90.0)
        };

        var result = SkyPlotControl.HasMovedBeyondThreshold(
            states, previousPositions, Cx, Cy, PlotRadius);

        return result;
    }

    /// <summary>
    /// When a satellite disappears (was in cache, not in new states), returns true.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Returns_true_when_satellite_disappears(PositiveInt seedRaw)
    {
        var rng = new Random(seedRaw.Get);

        // Cache two satellites
        var previousPositions = new Dictionary<string, (double X, double Y)>();
        for (var i = 0; i < 2; i++)
        {
            var az = rng.NextDouble() * 360.0;
            var el = rng.NextDouble() * 90.0;
            SkyPlotControl.TryAzElToPoint(Cx, Cy, PlotRadius, az, el, out var pt);
            previousPositions[$"SAT{i:D5}"] = pt;
        }

        // New states only include the first satellite at same position
        var firstId = "SAT00000";
        var prevFirst = previousPositions[firstId];

        // Reverse-compute az/el from the cached point (approximate)
        // Instead, just use the same coords: pass a satellite at the exact same screen pos
        // by using 0 az, 45 el as example, and cache that exact position
        var az1 = rng.NextDouble() * 360.0;
        var el1 = rng.NextDouble() * 90.0;
        SkyPlotControl.TryAzElToPoint(Cx, Cy, PlotRadius, az1, el1, out var pt1);
        previousPositions.Clear();
        previousPositions["SAT00000"] = pt1;
        previousPositions["SAT00001"] = (100.0, 100.0);

        // Only one satellite in new states, at the same cached position
        var states = new List<SatelliteTrackState> { MakeState("SAT00000", az1, el1) };

        var result = SkyPlotControl.HasMovedBeyondThreshold(
            states, previousPositions, Cx, Cy, PlotRadius);

        return result; // Should be true because sat count changed
    }

    /// <summary>
    /// When positions are identical (zero movement), returns false.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Returns_false_when_no_movement(PositiveInt seedRaw)
    {
        var rng = new Random(seedRaw.Get);
        var count = rng.Next(1, 6);

        var states = new List<SatelliteTrackState>();
        var previousPositions = new Dictionary<string, (double X, double Y)>();

        for (var i = 0; i < count; i++)
        {
            var id = $"SAT{i:D5}";
            var az = rng.NextDouble() * 360.0;
            var el = rng.NextDouble() * 90.0;

            states.Add(MakeState(id, az, el));

            SkyPlotControl.TryAzElToPoint(Cx, Cy, PlotRadius, az, el, out var point);
            previousPositions[id] = point;
        }

        var result = SkyPlotControl.HasMovedBeyondThreshold(
            states, previousPositions, Cx, Cy, PlotRadius);

        return !result; // No movement → should NOT render
    }

    /// <summary>
    /// Empty states with empty cache returns false (nothing changed).
    /// </summary>
    [Fact]
    public void Empty_states_empty_cache_returns_false()
    {
        var states = new List<SatelliteTrackState>();
        var previousPositions = new Dictionary<string, (double X, double Y)>();

        var result = SkyPlotControl.HasMovedBeyondThreshold(
            states, previousPositions, Cx, Cy, PlotRadius);

        Assert.False(result);
    }

    /// <summary>
    /// Empty states with non-empty cache returns true (all satellites gone).
    /// </summary>
    [Fact]
    public void Empty_states_nonempty_cache_returns_true()
    {
        var states = new List<SatelliteTrackState>();
        var previousPositions = new Dictionary<string, (double X, double Y)>
        {
            ["SAT00001"] = (100.0, 100.0)
        };

        var result = SkyPlotControl.HasMovedBeyondThreshold(
            states, previousPositions, Cx, Cy, PlotRadius);

        Assert.True(result);
    }
}
