using OscarWatch.Core.Models;

namespace OscarWatch.Core.Radio;

/// <summary>Maps satellite downlink/uplink frequencies to configured Flex SmartSDR antenna tokens.</summary>
public static class FlexAntennaPortResolver
{
    public static readonly string[] KnownTokens = ["ANT1", "ANT2", "RXA", "RXB", "XVTR"];

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
        return KnownTokens.Contains(trimmed, StringComparer.Ordinal)
            ? trimmed
            : null;
    }
}
