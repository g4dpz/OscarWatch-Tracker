using System.Text.RegularExpressions;

namespace OscarWatch.Core.Qrz;

public static partial class QrzCallsignHelper
{
    private static readonly HashSet<string> PortableSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "P", "M", "AM", "MM", "QRP", "A", "LH"
    };

    public static bool IsPlausible(string? callsign)
    {
        var lookup = ToLookupCall(callsign);
        return lookup.Length > 0 && PlausibleCallRegex().IsMatch(lookup);
    }

    public static string ToLookupCall(string? callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign))
            return "";

        var normalized = callsign.Trim().ToUpperInvariant();
        var slash = normalized.IndexOf('/');
        if (slash <= 0 || slash == normalized.Length - 1)
            return normalized;

        var prefix = normalized[..slash];
        var suffix = normalized[(slash + 1)..];
        if (PortableSuffixes.Contains(suffix))
            return prefix;

        return normalized;
    }

    [GeneratedRegex(@"^[A-Z0-9]{1,3}[0-9][A-Z0-9]{1,4}$", RegexOptions.CultureInvariant)]
    private static partial Regex PlausibleCallRegex();
}
