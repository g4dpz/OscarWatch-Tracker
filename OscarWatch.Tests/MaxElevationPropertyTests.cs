// Feature: performance-optimisations, Property 3: MaxElevationDeg equals sample maximum

using FsCheck.Xunit;
using OscarWatch.Core.Models;
using OscarWatch.Orbit;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 3.4**
///
/// Property-based tests verifying that <see cref="PassPolarPlotBuilder.Build"/>
/// produces a <c>MaxElevationDeg</c> value that equals the maximum <c>ElevationDeg</c>
/// found in the <c>Samples</c> array when <c>useFullPass</c> is false and samples are non-empty.
/// </summary>
public class MaxElevationPropertyTests
{
    private static readonly SatelliteCatalogEntry IssSatellite = new()
    {
        Name = "ISS (ZARYA)",
        NoradId = "25544",
        Line1 = "1 25544U 98067A   25205.51782528  .00016717  00000+0  10270-3 0  9993",
        Line2 = "2 25544  51.6416 247.4627 0006703 130.5360 325.0288 15.50415322908603"
    };

    private static readonly GroundStation London = new()
    {
        DisplayName = "London",
        LatitudeDeg = 51.5,
        LongitudeDeg = -0.1,
        AltitudeMetersAsl = 50,
        GridSquare = "IO91"
    };

    /// <summary>
    /// Property 3: MaxElevationDeg equals sample maximum.
    ///
    /// Uses real ISS TLE and a real pass prediction to build polar plot data with
    /// useFullPass = false (mutual-window-only mode). Asserts that MaxElevationDeg
    /// matches the maximum ElevationDeg found in the Samples collection within 0.001°.
    ///
    /// The property is tested with arbitrary offsets into the mutual window to exercise
    /// different sub-windows of the pass.
    /// </summary>
    [Property]
    public bool MaxElevationDeg_equals_sample_maximum_when_useFullPass_is_false(byte offsetByte)
    {
        // Use a fixed time window known to produce ISS passes over London
        var utcStart = new DateTime(2026, 5, 23, 0, 0, 0, DateTimeKind.Utc);
        var predictor = new BruteForcePassPredictor();
        var passes = predictor.GetPassesAsync(
            IssSatellite,
            London,
            utcStart,
            utcStart.AddDays(2),
            minimumElevationDeg: 5).GetAwaiter().GetResult();

        if (passes.Count == 0)
            return true; // vacuously true if no passes found

        var pass = passes[0];
        var propagator = new PublicOrbitToolsPropagator();
        propagator.LoadSatellite(IssSatellite);

        // Use the offset byte to generate different mutual windows within the pass
        var passDuration = (pass.LosUtc - pass.AosUtc).TotalSeconds;
        var startOffset = (offsetByte % 50) / 100.0 * passDuration;
        var endOffset = (offsetByte % 30) / 100.0 * passDuration;

        var mutualStart = pass.AosUtc.AddSeconds(startOffset);
        var mutualEnd = pass.LosUtc.AddSeconds(-endOffset);

        // Ensure mutual window is valid
        if (mutualEnd <= mutualStart)
            return true; // skip degenerate windows

        var plot = PassPolarPlotBuilder.Build(
            IssSatellite,
            propagator,
            London,
            pass,
            useFullPass: false,
            mutualStart,
            mutualEnd);

        if (plot.Samples.Count == 0)
            return true; // vacuously true if no samples

        var sampleMax = plot.Samples.Max(s => s.ElevationDeg);
        return Math.Abs(plot.MaxElevationDeg - sampleMax) < 0.001;
    }
}
