namespace OscarWatch.Core.Radio;

/// <summary>
/// How long the receive dial must stay still on linear USB/LSB/CW before OscarWatch
/// resumes Doppler CAT, and how long Main/Sub radios wait before uplink (Sub) CAT.
/// </summary>
public static class InteractiveDialResumePolicy
{
    /// <summary>Legacy eight-sample wait at the ~100 ms rig loop (8 × 100 ms).</summary>
    public const int DefaultSettleMs = 800;
    public const int MinSettleMs = 200;
    public const int MaxSettleMs = 5000;

    /// <summary>Defer Sub writes after an operator dial move so scanning does not select Sub.</summary>
    public const int DefaultUplinkResumeMs = 2500;
    public const int MinUplinkResumeMs = 500;
    public const int MaxUplinkResumeMs = 10000;

    /// <summary>
    /// 0 or negative (missing settings.json key) keeps the historical default.
    /// </summary>
    public static int ResolveSettleMs(int ms) =>
        ms <= 0 ? DefaultSettleMs : Math.Clamp(ms, MinSettleMs, MaxSettleMs);

    public static int ResolveUplinkResumeMs(int ms) =>
        ms <= 0 ? DefaultUplinkResumeMs : Math.Clamp(ms, MinUplinkResumeMs, MaxUplinkResumeMs);

    /// <summary>
    /// True when the dial has been still for the configured settle time, or when enough
    /// consecutive identical samples have been seen (rig loop ~100 ms, so 800 ms ≈ 8 samples).
    /// </summary>
    public static bool IsDialSettled(
        DateTime stableSinceUtc,
        DateTime nowUtc,
        int settleMs,
        int identicalSampleCount,
        int loopIntervalMs = 100)
    {
        var requiredMs = ResolveSettleMs(settleMs);
        if (stableSinceUtc != DateTime.MinValue
            && (nowUtc - stableSinceUtc).TotalMilliseconds >= requiredMs)
        {
            return true;
        }

        var interval = loopIntervalMs <= 0 ? 100 : loopIntervalMs;
        var requiredSamples = Math.Max(2, (requiredMs + interval - 1) / interval);
        return identicalSampleCount >= requiredSamples;
    }
}
