using System.Globalization;

namespace OscarWatch.Rotator;

/// <summary>
/// Green Heron RT-21 / Hy-Gain DCU-1 serial command formatting and reply parsing.
/// Each axis is an independent single-heading controller (Az-El uses two COM ports).
/// </summary>
internal static class GreenHeronRt21Codec
{
    /// <summary>Immediate goto with tenths (Hamlib RT-21 form). The CR executes without AM1.</summary>
    public static string FormatSetPosition(double headingDeg)
    {
        var heading = Math.Clamp(headingDeg, 0, 999.9);
        return string.Create(CultureInfo.InvariantCulture, $"AP1{heading:000.0}\r;");
    }

    /// <summary>RT-21 tenths query. Reply is typically <c>xxx.y;</c> (leading space pad possible).</summary>
    public static string FormatQueryTenths() => "BI1;";

    /// <summary>Stop rotation / flush command buffer (DCU-1 / Rotor-EZ).</summary>
    public static string FormatStop() => ";";

    public static bool TryParseHeading(string? response, out double headingDeg)
    {
        headingDeg = 0;
        if (string.IsNullOrWhiteSpace(response))
            return false;

        var text = response.Trim().TrimEnd(';').Trim();
        if (text.Length == 0)
            return false;

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out headingDeg))
            return false;

        if (headingDeg is 360.0 or 360)
            headingDeg = 0;

        // Allow 0–450 for overlap rotators configured in the RT-21 setup utility.
        return headingDeg is >= 0 and <= 450;
    }

    public static int? ToDisplayDegrees(double headingDeg) =>
        (int)Math.Clamp(Math.Round(headingDeg), 0, 450);
}
