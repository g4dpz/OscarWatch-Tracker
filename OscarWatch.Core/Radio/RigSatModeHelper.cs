namespace OscarWatch.Core.Radio;

public enum RigSatBandRegion
{
    Unknown,
    Hf,
    Vhf,
    UhfAndAbove
}

public static class RigSatModeHelper
{
    /// <summary>HF through 6m (IC-9100); below VHF satellite bands.</summary>
    private const double HfUpperKHz = 50_000;

    /// <summary>VHF vs UHF split used for pass-to-pass Main/Sub band swaps.</summary>
    private const double UhfLowerKHz = 400_000;

    /// <summary>
    /// True when Main/Sub satellite layout applies (real cross-band pass with both frequencies).
    /// Beacon/downlink-only modes (uplink 0) must not use this — |downlink − 0| would always exceed 10 MHz.
    /// </summary>
    public static bool UseMainSubLayout(double downlinkKHz, double uplinkKHz) =>
        downlinkKHz > 0 && uplinkKHz > 0 && Math.Abs(downlinkKHz - uplinkKHz) > 10_000;

    /// <summary>
    /// Nominal uplink and downlink centre frequencies are equal (e.g. ISS Packet 145.825 MHz).
    /// The radio still needs split — Doppler separates TX and RX during the pass.
    /// </summary>
    public static bool IsSameBandSimplex(double downlinkKHz, double uplinkKHz) =>
        downlinkKHz > 0 && uplinkKHz > 0 && Math.Abs(downlinkKHz - uplinkKHz) < 0.001;

    /// <summary>True when downlink centre is below the UHF satellite band (HF or VHF).</summary>
    public static bool IsVhfCenterKHz(double kHz) => kHz is > 0 and < UhfLowerKHz;

    public static bool IsUhfCenterKHz(double kHz) => kHz >= UhfLowerKHz;

    public static bool IsHfCenterKHz(double kHz) => kHz is > 0 and < HfUpperKHz;

    public static RigSatBandRegion GetSatBandRegion(double kHz) =>
        kHz switch
        {
            <= 0 => RigSatBandRegion.Unknown,
            < HfUpperKHz => RigSatBandRegion.Hf,
            < UhfLowerKHz => RigSatBandRegion.Vhf,
            _ => RigSatBandRegion.UhfAndAbove
        };

    /// <summary>
    /// True when Main is on the wrong band for <paramref name="downlinkKHz"/>.
    /// Matches IC-910/9100/9700 satellite Main=RX, Sub=TX layout (HF/VHF/UHF).
    /// </summary>
    public static bool NeedsMainSubBandSwap(long mainFrequencyHz, double downlinkKHz)
    {
        if (downlinkKHz <= 0 || mainFrequencyHz <= 0)
            return false;

        var downlinkRegion = GetSatBandRegion(downlinkKHz);
        if (downlinkRegion == RigSatBandRegion.Unknown)
            return false;

        var mainRegion = GetSatBandRegion(mainFrequencyHz / 1000.0);
        return mainRegion != downlinkRegion;
    }
}
