namespace OscarWatch.Core.Models;

/// <summary>
/// Classifies operator dial vs automatic CAT for Doppler pass log CSV column <c>DialTracking</c>.
/// </summary>
public static class DopplerDialTrackingMode
{
    /// <summary>FM, data, or other non-interactive path — Doppler every loop.</summary>
    public const string Automatic = "automatic";

    /// <summary>Linear USB/LSB/CW: Main matches last CAT RX — hands off the dial.</summary>
    public const string HandsOff = "hands_off";

    /// <summary>Linear: dial still moving or stability timer not complete — CAT paused.</summary>
    public const string DialWait = "dial_wait";

    /// <summary>Linear: dial stable but off the Doppler/CAT baseline — passband hunt.</summary>
    public const string DialTrack = "dial_track";

    public static string Resolve(bool interactive, bool handsOffAutomatic, bool dialStable)
    {
        if (!interactive)
            return Automatic;

        if (handsOffAutomatic)
            return HandsOff;

        if (!dialStable)
            return DialWait;

        return DialTrack;
    }
}
