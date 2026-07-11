using System.Globalization;
using OscarWatch.Core.Models;
using OscarWatch.Core.Tle;
using OscarWatch.Localization;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 6.2, 6.3, 6.4, 6.5**
///
/// Example-based and edge-case tests verifying altitude estimation behaviour
/// of <see cref="TleAltitude"/>: ISS reference value, short line 2 handling,
/// zero/negative mean motion, and fallback when reported altitude is below 100 km.
/// </summary>
public sealed class TleAltitudeTests
{
    /// <summary>
    /// ISS mean motion (~15.5 rev/day) yields an altitude within 50 km of 410 km.
    /// </summary>
    [Fact]
    public void Iss_mean_motion_yields_altitude_within_50km_of_410()
    {
        // ISS mean motion is approximately 15.5 rev/day
        var entry = MakeEntryWithMeanMotion(15.5);

        var altitude = TleAltitude.EstimateFromMeanMotion(entry);

        Assert.InRange(altitude, 360.0, 460.0);
    }

    /// <summary>
    /// Line 2 shorter than 63 characters returns 0 from EstimateFromMeanMotion.
    /// </summary>
    [Fact]
    public void Short_line2_returns_zero()
    {
        var entry = new SatelliteCatalogEntry
        {
            Name = "TEST",
            NoradId = "00001",
            Line1 = new string('0', 69),
            Line2 = "2 25544U" // Only 8 characters — well below 63
        };

        var altitude = TleAltitude.EstimateFromMeanMotion(entry);

        Assert.Equal(0.0, altitude);
    }

    /// <summary>
    /// Zero mean motion in line 2 returns 0 from EstimateFromMeanMotion.
    /// </summary>
    [Fact]
    public void Zero_mean_motion_returns_zero()
    {
        var entry = MakeEntryWithMeanMotion(0.0);

        var altitude = TleAltitude.EstimateFromMeanMotion(entry);

        Assert.Equal(0.0, altitude);
    }

    /// <summary>
    /// Negative mean motion in line 2 returns 0 from EstimateFromMeanMotion.
    /// </summary>
    [Fact]
    public void Negative_mean_motion_returns_zero()
    {
        var entry = MakeEntryWithMeanMotion(-5.0);

        var altitude = TleAltitude.EstimateFromMeanMotion(entry);

        Assert.Equal(0.0, altitude);
    }

    /// <summary>
    /// When reported altitude is below 100 km, ResolveAltitudeKm falls back
    /// to EstimateFromMeanMotion and returns the TLE-based estimate.
    /// </summary>
    [Fact]
    public void Reported_altitude_below_100_triggers_fallback_to_mean_motion()
    {
        // Use ISS mean motion so fallback produces a valid altitude
        var entry = MakeEntryWithMeanMotion(15.5);

        var result = TleAltitude.ResolveAltitudeKm(50.0, entry);

        // The fallback should return the TLE estimate (around 410 km), not 50
        Assert.True(result > 100.0, $"Expected fallback altitude > 100 km, got {result}");
        Assert.InRange(result, 360.0, 460.0);
    }

    /// <summary>
    /// TLE mean motion uses a dot decimal separator; Spanish UI culture must not break parsing.
    /// </summary>
    [Fact]
    public void EstimateFromMeanMotion_parses_iss_tle_under_spanish_ui_culture()
    {
        using var _ = TestUiCulture.Apply("es");

        var entry = new SatelliteCatalogEntry
        {
            Name = "ISS",
            NoradId = "25544",
            Line1 = new string('0', 69),
            Line2 = "2 25544  51.6400 247.4627 0006703 130.5360 325.0288 15.49519779439320"
        };

        var altitude = TleAltitude.EstimateFromMeanMotion(entry);

        Assert.InRange(altitude, 360.0, 460.0);
    }

    /// <summary>
    /// Creates a minimal SatelliteCatalogEntry with a valid 69-character line 2
    /// containing the specified mean motion value at positions 52–62.
    /// </summary>
    private static SatelliteCatalogEntry MakeEntryWithMeanMotion(double meanMotion)
    {
        var meanMotionStr = meanMotion.ToString("00.00000000", CultureInfo.InvariantCulture);
        if (meanMotionStr.Length > 11)
            meanMotionStr = meanMotionStr[..11];
        else
            meanMotionStr = meanMotionStr.PadLeft(11);

        const string line2Template = "2 25544  51.6400 247.4627 0006703 130.5360 325.0288 15.49519779439320";
        var line2 = string.Concat(line2Template.AsSpan(0, 52), meanMotionStr, line2Template.AsSpan(63));

        return new SatelliteCatalogEntry
        {
            Name = "TEST",
            NoradId = "00001",
            Line1 = new string('0', 69),
            Line2 = line2
        };
    }
}
