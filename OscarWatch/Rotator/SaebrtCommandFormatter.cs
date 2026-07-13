using System.Globalization;

namespace OscarWatch.Rotator;

/// <summary>
/// Formats SAEBRTrack (EasyComm I style) position commands.
/// Matches Hamlib <c>ROT_MODEL_SAEBRTRACK</c>: AZ/EL with one decimal place plus UP/DN placeholders.
/// </summary>
public static class SaebrtCommandFormatter
{
    private const string PlaceholderSuffix = " UP000 XXX DN000 XXX";

    /// <summary>Build a combined AZ/EL move command (line feed not included).</summary>
    public static string FormatSetPosition(double azimuthDeg, double elevationDeg)
    {
        var az = FormatAngle(azimuthDeg);
        var el = FormatAngle(elevationDeg);
        return string.Create(CultureInfo.InvariantCulture, $"AZ{az} EL{el}{PlaceholderSuffix}");
    }

    /// <summary>Build a compact AZ/EL move command for controllers that expect concatenated fields (e.g. PSR-100).</summary>
    public static string FormatCompactSetPosition(double azimuthDeg, double elevationDeg)
    {
        var az = FormatAngle(azimuthDeg);
        var el = FormatAngle(elevationDeg);
        return string.Create(CultureInfo.InvariantCulture, $"AZ{az}EL{el}");
    }

    internal static string FormatAngle(double degrees) =>
        degrees.ToString("000.0", CultureInfo.InvariantCulture);
}
