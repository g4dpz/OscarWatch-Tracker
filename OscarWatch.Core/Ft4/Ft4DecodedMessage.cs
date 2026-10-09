namespace OscarWatch.Core.Ft4;

/// <summary>One FT4 band-activity line (RX decode, own echo, or our TX).</summary>
public sealed record Ft4DecodedMessage(
    DateTime SlotUtc,
    string Text,
    float FreqHz,
    float TimeSec,
    float SnrDb,
    string? CallTo,
    string? CallDe,
    string? Extra,
    bool IsOwnEcho,
    bool IsTransmitted = false,
    bool IsApriori = false,
    bool IsRejected = false)
{
    /// <summary>True for ordinary RX lines (not our TX and not our own uplink echo).</summary>
    public bool IsReceiveActivity => !IsTransmitted && !IsOwnEcho;

    /// <summary>A hinted reply that passed the CRC and has not been contradicted.</summary>
    public bool IsAcceptedApriori => IsApriori && !IsRejected;

    /// <summary>
    /// A hinted reply that is not used. The CRC did not confirm it, or a later decode
    /// of the same station in this slot showed a different message.
    /// </summary>
    public bool IsRejectedApriori => IsApriori && IsRejected;
}
