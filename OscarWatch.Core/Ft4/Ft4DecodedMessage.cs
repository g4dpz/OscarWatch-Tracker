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
    bool IsTransmitted = false);
