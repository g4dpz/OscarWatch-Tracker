namespace OscarWatch.Core.Cloudlog;

/// <summary>Decides when to POST to Cloudlog radio API: on state change or keepalive only.</summary>
public static class CloudlogRadioPublishPolicy
{
    public const int DefaultKeepaliveIntervalMs = 600_000;

    public const int MinKeepaliveIntervalMs = 60_000;

    public const int MaxKeepaliveIntervalMs = 3_600_000;

    public static int NormalizeKeepaliveIntervalMs(int configuredMs)
    {
        if (configuredMs <= 0)
            return DefaultKeepaliveIntervalMs;

        return Math.Clamp(configuredMs, MinKeepaliveIntervalMs, MaxKeepaliveIntervalMs);
    }

    /// <summary>Migrate legacy 1 s throttle default to 10 min keepalive.</summary>
    public static int MigrateKeepaliveIntervalMs(int storedMs) =>
        storedMs <= 0 ? DefaultKeepaliveIntervalMs
        : storedMs == 1000 ? DefaultKeepaliveIntervalMs
        : NormalizeKeepaliveIntervalMs(storedMs);

    public static bool ShouldPost(
        string? lastSignature,
        string signature,
        DateTime lastPostUtc,
        DateTime utcNow,
        int keepaliveIntervalMs)
    {
        if (!string.Equals(lastSignature, signature, StringComparison.Ordinal))
            return true;

        var keepalive = NormalizeKeepaliveIntervalMs(keepaliveIntervalMs);
        return (utcNow - lastPostUtc).TotalMilliseconds >= keepalive;
    }
}
