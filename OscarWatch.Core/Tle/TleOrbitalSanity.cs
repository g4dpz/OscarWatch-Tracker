using System.Globalization;
using OscarWatch.Core.Models;

namespace OscarWatch.Core.Tle;

/// <summary>
/// Rejects orbital elements that parse but are implausible for amateur satellites.
/// Catches corrupt cache/network data that would propagate at absurd speeds.
/// </summary>
public static class TleOrbitalSanity
{
    // GEO ~1.0 rev/day; very low LEO ~16–17; MEO amateur sats ~11–13.
    public const double MinMeanMotionRevPerDay = 0.1;
    public const double MaxMeanMotionRevPerDay = 18.0;

    public const double MaxEccentricity = 0.99;

    public static bool IsGpRecordPlausible(GpElementRecord record) =>
        record.MeanMotion is > MinMeanMotionRevPerDay and <= MaxMeanMotionRevPerDay
        && record.Inclination is >= 0 and <= 180
        && record.Eccentricity is >= 0 and <= MaxEccentricity;

    public static bool IsEntryPlausible(SatelliteCatalogEntry entry) =>
        TryReadLine2Elements(entry.Line2, out var inclination, out var eccentricity, out var meanMotion)
        && meanMotion > MinMeanMotionRevPerDay
        && meanMotion <= MaxMeanMotionRevPerDay
        && inclination is >= 0 and <= 180
        && eccentricity is >= 0 and <= MaxEccentricity;

    internal static bool TryReadLine2Elements(
        string line2,
        out double inclination,
        out double eccentricity,
        out double meanMotion)
    {
        inclination = 0;
        eccentricity = 0;
        meanMotion = 0;

        if (line2.Length < 63 || line2[0] != '2')
            return false;

        try
        {
            inclination = double.Parse(line2.AsSpan(8, 8), CultureInfo.InvariantCulture);
            var eccentricityField = line2.AsSpan(26, 7);
            eccentricity = double.Parse(
                string.Concat("0.", eccentricityField),
                CultureInfo.InvariantCulture);
            meanMotion = double.Parse(line2.AsSpan(52, 11), CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
