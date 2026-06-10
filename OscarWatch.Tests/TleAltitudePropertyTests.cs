// Feature: test-coverage-expansion, Property 16: Altitude Passthrough
// Feature: test-coverage-expansion, Property 17: Estimated Altitude Non-Negative

using FsCheck.Xunit;
using OscarWatch.Core.Models;
using OscarWatch.Core.Tle;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 6.1, 6.6**
///
/// Property-based tests verifying altitude estimation invariants of
/// <see cref="TleAltitude"/>: passthrough for high reported altitudes
/// and non-negativity of mean-motion-based estimates.
/// </summary>
public class TleAltitudePropertyTests
{
    /// <summary>
    /// Property 16: Altitude Passthrough.
    ///
    /// For any reported altitude >= 100 km and any SatelliteCatalogEntry,
    /// ResolveAltitudeKm returns the reported altitude unchanged.
    /// </summary>
    [Property]
    public bool Reported_altitude_at_or_above_100_is_returned_unchanged(double rawAltitude, int noradId)
    {
        if (!IsFinite(rawAltitude))
            return true; // skip non-finite inputs

        // Constrain to >= 100 km
        var reportedAltitude = 100.0 + Math.Abs(rawAltitude % 100_000.0);

        var entry = MakeSatelliteEntry(noradId);

        var result = TleAltitude.ResolveAltitudeKm(reportedAltitude, entry);

        return result == reportedAltitude;
    }

    /// <summary>
    /// Property 17: Estimated Altitude Non-Negative.
    ///
    /// For any SatelliteCatalogEntry with a valid line 2 containing a positive
    /// mean motion, EstimateFromMeanMotion returns a value >= 0.
    /// </summary>
    [Property]
    public bool EstimateFromMeanMotion_returns_non_negative_for_valid_mean_motions(double rawMeanMotion)
    {
        if (!IsFinite(rawMeanMotion))
            return true; // skip non-finite inputs

        // Constrain to a positive mean motion (rev/day); realistic range is roughly 0.5 to 17
        var meanMotion = 0.5 + Math.Abs(rawMeanMotion % 16.5);

        var entry = MakeEntryWithMeanMotion(meanMotion);

        var result = TleAltitude.EstimateFromMeanMotion(entry);

        return result >= 0.0;
    }

    /// <summary>
    /// Creates a minimal SatelliteCatalogEntry with a valid 69-character line 2
    /// containing the specified mean motion value at positions 52–62.
    /// </summary>
    private static SatelliteCatalogEntry MakeEntryWithMeanMotion(double meanMotion)
    {
        // Format mean motion as 11 characters right-aligned (positions 52–62 of line 2)
        var meanMotionStr = meanMotion.ToString("00.00000000");
        // Pad or truncate to exactly 11 characters
        if (meanMotionStr.Length > 11)
            meanMotionStr = meanMotionStr[..11];
        else
            meanMotionStr = meanMotionStr.PadLeft(11);

        // Build a 69-character line 2 with the mean motion at the correct position
        // Positions 0–51: padding, positions 52–62: mean motion, positions 63–68: remaining
        var line2 = new string('0', 52) + meanMotionStr + "000000";

        return new SatelliteCatalogEntry
        {
            Name = "TEST",
            NoradId = "00001",
            Line1 = new string('0', 69),
            Line2 = line2
        };
    }

    private static SatelliteCatalogEntry MakeSatelliteEntry(int noradId)
    {
        return new SatelliteCatalogEntry
        {
            Name = "TEST",
            NoradId = Math.Abs(noradId % 99999).ToString("D5"),
            Line1 = new string('0', 69),
            Line2 = new string('0', 69)
        };
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
