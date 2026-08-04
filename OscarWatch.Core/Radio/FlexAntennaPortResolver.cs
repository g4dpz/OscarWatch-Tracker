using OscarWatch.Core.Models;

namespace OscarWatch.Core.Radio;

/// <summary>Maps satellite downlink/uplink frequencies to configured Flex SmartSDR antenna tokens.</summary>
public static class FlexAntennaPortResolver
{
    /// <summary>
    /// Offline / baseline SmartSDR tokens (dual-XVTR radios use XVTA/XVTB; single-port may report XVTR).
    /// </summary>
    public static readonly string[] KnownTokens =
        ["ANT1", "ANT2", "RX_A", "RX_B", "XVTA", "XVTB", "XVTR"];

    public static string? ResolveRxPort(RigSettings settings, long frequencyHz)
    {
        if (frequencyHz <= 0)
            return null;

        var token = RigSatModeHelper.IsVhfCenterKHz(frequencyHz / 1000.0)
            ? settings.FlexVhfRxAnt
            : settings.FlexUhfRxAnt;
        return NormalizeToken(token);
    }

    public static string? ResolveTxPort(RigSettings settings, long frequencyHz)
    {
        if (frequencyHz <= 0)
            return null;

        var token = RigSatModeHelper.IsVhfCenterKHz(frequencyHz / 1000.0)
            ? settings.FlexVhfTxAnt
            : settings.FlexUhfTxAnt;
        return NormalizeToken(token);
    }

    public static string? NormalizeToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var trimmed = token.Trim().ToUpperInvariant();

        // Legacy wiki-style tokens from the first Settings build.
        trimmed = trimmed switch
        {
            "RXA" => "RX_A",
            "RXB" => "RX_B",
            "XVTR_A" or "XVTRA" => "XVTA",
            "XVTR_B" or "XVTRB" => "XVTB",
            _ => trimmed
        };

        if (KnownTokens.Contains(trimmed, StringComparer.Ordinal))
            return trimmed;

        // Accept radio-reported ports (future Flex names) that look like SmartSDR tokens.
        return IsValidAntennaToken(trimmed) ? trimmed : null;
    }

    /// <summary>Operator-facing label for a SmartSDR antenna token.</summary>
    public static string FormatDisplayLabel(string token)
    {
        var normalized = NormalizeToken(token) ?? token.Trim().ToUpperInvariant();
        return normalized switch
        {
            "RX_A" => "RX A",
            "RX_B" => "RX B",
            "XVTA" => "XVTR A",
            "XVTB" => "XVTR B",
            _ => normalized
        };
    }

    /// <summary>
    /// Merges radio-reported ports with the offline baseline (radio order first, then any missing baseline tokens).
    /// </summary>
    public static IReadOnlyList<string> MergeAntennaTokens(IEnumerable<string>? radioTokens)
    {
        var merged = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(string? raw)
        {
            var token = NormalizeToken(raw);
            if (token is null || !seen.Add(token))
                return;
            merged.Add(token);
        }

        if (radioTokens is not null)
        {
            foreach (var token in radioTokens)
                Add(token);
        }

        foreach (var token in KnownTokens)
            Add(token);

        return merged;
    }

    private static bool IsValidAntennaToken(string token)
    {
        if (token.Length is < 2 or > 16)
            return false;

        foreach (var ch in token)
        {
            if (ch is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_')
                continue;
            return false;
        }

        return true;
    }
}
