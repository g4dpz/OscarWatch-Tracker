using System.Globalization;

namespace OscarWatch.Rotator;

/// <summary>
/// Formats SAEBRTrack position commands as compact whole-degree <c>AZnnnELnnn</c>
/// (Arduino / SatPC32 style; no spaces or UP/DN placeholders).
/// </summary>
public static class SaebrtCommandFormatter
{
    /// <summary>Build a compact AZ/EL move command (line feed not included).</summary>
    public static string FormatSetPosition(double azimuthDeg, double elevationDeg)
    {
        var az = FormatWholeDegrees(azimuthDeg);
        var el = FormatWholeDegrees(elevationDeg);
        return string.Create(CultureInfo.InvariantCulture, $"AZ{az}EL{el}");
    }

    internal static string FormatWholeDegrees(double degrees) =>
        ((int)Math.Round(degrees, MidpointRounding.AwayFromZero))
            .ToString("000", CultureInfo.InvariantCulture);
}
