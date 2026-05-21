namespace OscarWatch.Rotator;

/// <summary>
/// Parses Yaesu GS-232 position responses: <c>AZ=aaa EL=eee</c>, or just <c>AZ=aaa</c>, or just <c>EL=eee</c>.
/// </summary>
public static class Gs232PositionParser
{
    /// <summary>Extract any AZ=/EL= tokens from a response line (C2 may return one or both).</summary>
    public static void TryParseParts(string? response, out int? azimuth, out int? elevation)
    {
        azimuth = null;
        elevation = null;
        if (string.IsNullOrWhiteSpace(response))
            return;

        foreach (var part in response.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryParseAzimuthToken(part, out var azValue))
                azimuth = azValue;
            else if (TryParseElevationToken(part, out var elValue))
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

    public static bool TryParseAzimuthLine(string? response, out int azimuth)
    {
        TryParseParts(NormalizeLine(response), out var az, out _);
        if (az is null)
        {
            azimuth = 0;
            return false;
        }

        azimuth = az.Value;
        return true;
    }

    public static bool TryParseElevationLine(string? response, out int elevation)
    {
        TryParseParts(NormalizeLine(response), out _, out var el);
        if (el is null)
        {
            elevation = 0;
            return false;
        }

        elevation = el.Value;
        return true;
    }

    private static string? NormalizeLine(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return null;

        var line = response.Trim();
        var firstBreak = line.IndexOfAny(['\r', '\n']);
        return firstBreak >= 0 ? line[..firstBreak].Trim() : line;
    }

    private static bool TryParseAzimuthToken(string token, out int azimuth)
    {
        azimuth = 0;
        if (!token.StartsWith("AZ=", StringComparison.OrdinalIgnoreCase))
            return false;

        return int.TryParse(token.AsSpan(3), out azimuth);
    }

    private static bool TryParseElevationToken(string token, out int elevation)
    {
        elevation = 0;
        if (!token.StartsWith("EL=", StringComparison.OrdinalIgnoreCase))
            return false;

        return int.TryParse(token.AsSpan(3), out elevation);
    }
}
