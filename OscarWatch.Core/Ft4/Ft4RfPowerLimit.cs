namespace OscarWatch.Core.Ft4;

/// <summary>
/// FT4 uplink power policy. Satellite FT4 must stay modest; OscarWatch blocks TX
/// when the radio reports a set RF power above this limit (where readable).
/// </summary>
public static class Ft4RfPowerLimit
{
    /// <summary>Maximum allowed set RF power for FT4 transmit (watts).</summary>
    public const double MaxWatts = 30.0;

    public const string StatusKey = "Ft4.Blocked.RfPowerTooHigh";

    public static bool ExceedsLimit(double watts) => watts > MaxWatts;
}
