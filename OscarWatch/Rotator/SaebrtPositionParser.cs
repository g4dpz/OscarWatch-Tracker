using System.Globalization;

namespace OscarWatch.Rotator;

/// <summary>
/// Parses SAEBRTrack / EasyComm I style position replies when hardware provides feedback.
/// Supports spaced (<c>AZ120.5 EL45.0</c>) and compact (<c>AZ120.5EL45.0</c>) forms.
/// </summary>
public static class SaebrtPositionParser
{
    public static void TryParseParts(string? response, out int? azimuth, out int? elevation)
    {
        azimuth = null;
        elevation = null;
        var line = NormalizeLine(response);
        if (line is null)
            return;

        if (TryParseCompact(line, out var compactAz, out var compactEl))
        {
            azimuth = compactAz;
            elevation = compactEl;
            return;
        }

        foreach (var token in line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryParseAxisToken(token, "AZ", out var azValue))
                azimuth = azValue;
            else if (TryParseAxisToken(token, "EL", out var elValue))
                elevation = elValue;
        }
    }

    public static bool TryParseCombined(string? response, out int azimuth, out int elevation)
    {
        TryParseParts(response, out var az, out var el);
        if (az is null || el is null)
        {
            azimuth = 0;
            elevation = 0;
            return false;
        }

        azimuth = az.Value;
        elevation = el.Value;
        return true;
    }

    public static int? TryParseAxis(string? response, string axis)
    {
        TryParseParts(response, out var az, out var el);
        return string.Equals(axis, "AZ", StringComparison.OrdinalIgnoreCase) ? az : el;
    }

    private static string? NormalizeLine(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return null;

        var line = response.Trim();
        var firstBreak = line.IndexOfAny(['\r', '\n']);
        return firstBreak >= 0 ? line[..firstBreak].Trim() : line;
    }

    private static bool TryParseCompact(string line, out int? azimuth, out int? elevation)
    {
        azimuth = null;
        elevation = null;

        var elIndex = line.IndexOf("EL", StringComparison.OrdinalIgnoreCase);
        if (elIndex <= 2 || !line.StartsWith("AZ", StringComparison.OrdinalIgnoreCase))
            return false;

        var azText = line[2..elIndex];
        var elText = line[(elIndex + 2)..];
        if (!TryParseAngle(azText, out var az) || !TryParseAngle(elText, out var el))
            return false;

        azimuth = (int)Math.Round(az);
        elevation = (int)Math.Round(el);
        return true;
    }

    private static bool TryParseAxisToken(string token, string axis, out int degrees)
    {
        degrees = 0;
        if (!token.StartsWith(axis, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!TryParseAngle(token.AsSpan(axis.Length), out var angle))
            return false;

        degrees = (int)Math.Round(angle);
        return true;
    }

    private static bool TryParseAngle(ReadOnlySpan<char> text, out double angle)
    {
        angle = 0;
        var trimmed = text.Trim();
        return trimmed.Length > 0
            && double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out angle);
    }
}
