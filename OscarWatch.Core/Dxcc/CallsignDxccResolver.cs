using OscarWatch.Core.Geo;

namespace OscarWatch.Core.Dxcc;

/// <summary>
/// Resolves a callsign against a parsed cty.dat database, including portable prefix/suffix forms.
/// </summary>
public sealed class CallsignDxccResolver
{
    private static readonly HashSet<string> NonEntitySuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "P", "M", "MM", "AM", "QRP", "A", "LH", "BM", "J", "T", "B", "FF", "F",
        "AGR", "RPT", "MOBILE", "PORTABLE"
    };

    private readonly CtyDatDatabase _database;

    public CallsignDxccResolver(CtyDatDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public bool TryMatch(string? callsign, out CtyMatch match)
    {
        match = default;
        var call = MaidenheadLocator.NormalizeCallsign(callsign);
        if (call.Length == 0)
            return false;

        call = StripNonEntitySuffixes(call);
        if (call.Length == 0)
            return false;

        // PREFIX/HOME or PREFIX/HOME/… → entity from leading location prefix when it looks like a DX prefix.
        if (call.Contains('/', StringComparison.Ordinal))
        {
            var parts = call.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2)
            {
                var leading = parts[0];
                var home = parts[1];
                if (LooksLikeLocationPrefix(leading, home) && TryMatchCore(leading, out match))
                    return true;

                // Fall back to home call (e.g. odd /suffix that is not a known portable designator).
                if (TryMatchCore(home, out match))
                    return true;
            }
        }

        return TryMatchCore(call, out match);
    }

    private bool TryMatchCore(string call, out CtyMatch match)
    {
        match = default;
        if (call.Length == 0)
            return false;

        if (_database.ExactCalls.TryGetValue(call, out var exactEntity))
        {
            match = new CtyMatch(exactEntity, call, IsExactCall: true);
            return true;
        }

        var skipKg4 = call.StartsWith("KG4", StringComparison.Ordinal) && call.Length != 5;

        foreach (var entry in _database.PrefixesByLengthDescending)
        {
            if (skipKg4 && entry.Prefix == "KG4")
                continue;

            if (call.StartsWith(entry.Prefix, StringComparison.Ordinal))
            {
                match = new CtyMatch(entry.Entity, entry.Prefix, IsExactCall: false);
                return true;
            }
        }

        return false;
    }

    private static string StripNonEntitySuffixes(string call)
    {
        while (true)
        {
            var slash = call.LastIndexOf('/');
            if (slash <= 0 || slash >= call.Length - 1)
                return call;

            var suffix = call[(slash + 1)..];
            if (!IsNonEntitySuffix(suffix))
                return call;

            call = call[..slash];
        }
    }

    private static bool IsNonEntitySuffix(string suffix)
    {
        if (suffix.Length == 1 && char.IsDigit(suffix[0]))
            return true;

        return NonEntitySuffixes.Contains(suffix);
    }

    /// <summary>
    /// True when the left side is a short DXCC-style prefix and the right side looks like a full callsign
    /// (covers MM/M0VXX, EA8/G0ABC). False for W1AW/4 after digit suffixes are stripped elsewhere.
    /// </summary>
    private static bool LooksLikeLocationPrefix(string leading, string home)
    {
        if (leading.Length == 0 || leading.Length > 4)
            return false;

        if (home.Length < 3)
            return false;

        // Leading must contain a letter (not a bare digit zone).
        if (!leading.Any(char.IsLetter))
            return false;

        // Prefer cases where home looks longer/more "call-like" than the prefix.
        return home.Length >= leading.Length;
    }
}
