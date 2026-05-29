namespace OscarWatch.Core.Models;

/// <summary>
/// Which radio legs receive automatic Doppler correction during CAT tracking.
/// </summary>
public enum DopplerStrategy
{
    /// <summary>Correct uplink and downlink (SatPC32 full Doppler).</summary>
    Full,

    /// <summary>Downlink/RX only — uplink/TX fixed (SatPC32 TX-Fixed).</summary>
    DownlinkOnly,

    /// <summary>Uplink/TX only — downlink/RX fixed (SatPC32 RX-Fixed).</summary>
    UplinkOnly
}
