namespace OscarWatch.Core.Radio;

public static class RigSatModeHelper
{
    /// <summary>
    /// True when Main/Sub satellite layout applies (real cross-band pass with both frequencies).
    /// Beacon/downlink-only modes (uplink 0) must not use this — |downlink − 0| would always exceed 10 MHz.
    /// </summary>
    public static bool UseMainSubLayout(double downlinkKHz, double uplinkKHz) =>
        downlinkKHz > 0 && uplinkKHz > 0 && Math.Abs(downlinkKHz - uplinkKHz) > 10_000;

    public static bool IsVhfCenterKHz(double kHz) => kHz is > 0 and < 400_000;

    public static bool IsUhfCenterKHz(double kHz) => kHz >= 400_000;

    /// <summary>
    /// True when Main is on the wrong band for <paramref name="downlinkKHz"/> (2m vs 70cm).
    /// Matches IC-910/9700 satellite Main=RX, Sub=TX layout.
    /// </summary>
    public static bool NeedsMainSubBandSwap(long mainFrequencyHz, double downlinkKHz)
    {
        if (downlinkKHz <= 0 || mainFrequencyHz <= 0)
            return false;

        var downlinkOnVhf = IsVhfCenterKHz(downlinkKHz);
        var mainOnUhf = mainFrequencyHz > 400_000_000;
        var mainOnVhf = mainFrequencyHz < 200_000_000;

        return downlinkOnVhf switch
        {
            true when mainOnUhf => true,
            false when mainOnVhf && IsUhfCenterKHz(downlinkKHz) => true,
            _ => false
        };
    }
}
