namespace OscarWatch.Core.Ft4;

/// <summary>
/// The TX RF power check is a CAT round trip, and on an ICOM it also selects the uplink
/// and restores the downlink. The receive burst in the slot before ours starts at 0.5 s
/// and lasts <see cref="Ft4SlotClock.Ft4SymbolBurstSeconds"/>, so it is over by about 6 s
/// even when the copy is a little late. Starting 1.5 s before our slot sits in that quiet
/// tail, with time left for the radio to answer. If it has not answered when the slot
/// opens, transmission starts anyway. A finished reading over the limit still blocks the slot.
/// </summary>
public static class Ft4RfPowerLead
{
    public static readonly TimeSpan Lead = TimeSpan.FromSeconds(1.5);

    /// <summary>When to start the RF power read for a TX slot opening at <paramref name="slotStartUtc"/>.</summary>
    public static DateTime CheckAtUtc(DateTime slotStartUtc) => slotStartUtc - Lead;

    /// <summary>
    /// True when this slot may transmit. An unfinished read does not hold the slot.
    /// A finished refusal still blocks it.
    /// </summary>
    public static bool AwaitVerdict(Task<bool> check)
    {
        if (!check.IsCompleted)
            return true;
        if (check.IsCompletedSuccessfully)
            return check.Result;

        _ = check.Exception;
        return true;
    }
}
