using System.Globalization;

namespace OscarWatch.Rotator;

internal static class EasyCommPositionParser
{
    public static int? TryParseAxis(string? response, string axis)
    {
        if (string.IsNullOrWhiteSpace(response))
            return null;

        var token = response.Trim();
        if (token.StartsWith(axis, StringComparison.OrdinalIgnoreCase))
            token = token[axis.Length..].Trim();

        if (token.StartsWith('='))
            token = token[1..].Trim();

        if (token.EndsWith(';'))
            token = token[..^1].Trim();

        if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var angle)
            && !double.TryParse(token, NumberStyles.Float, CultureInfo.CurrentCulture, out angle))
            return null;

        return (int)Math.Round(angle);
    }
}
