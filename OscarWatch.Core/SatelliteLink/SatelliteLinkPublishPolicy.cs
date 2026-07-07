namespace OscarWatch.Core.SatelliteLink;

public static class SatelliteLinkPublishPolicy
{
    public static bool ShouldBroadcast(
        string? lastSignature,
        string signature,
        DateTime lastBroadcastUtc,
        DateTime utcNow,
        int updateIntervalMs,
        bool force)
    {
        if (force)
            return true;

        if (!string.Equals(lastSignature, signature, StringComparison.Ordinal))
            return true;

        var interval = Models.SatelliteLinkSettings.NormalizeUpdateIntervalMs(updateIntervalMs);
        return (utcNow - lastBroadcastUtc).TotalMilliseconds >= interval;
    }
}
