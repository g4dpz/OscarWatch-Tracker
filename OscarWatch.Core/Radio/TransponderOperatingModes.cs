using OscarWatch.Core.Models;

namespace OscarWatch.Core.Radio;

/// <summary>Voice/CW operating-style overrides for linear SSB transponder modes.</summary>
public static class TransponderOperatingModes
{
    public static bool SupportsCwUplinkToggle(SatelliteTransponderMode mode)
    {
        if (mode.IsBeaconOnly || mode.UplinkKHz <= 0 || mode.IsFmMode)
            return false;

        if (IsCw(mode.UplinkMode))
            return false;

        return IsLinearSideband(mode.DownlinkMode) && IsLinearSideband(mode.UplinkMode);
    }

    public static string GetEffectiveUplinkMode(SatelliteTransponderMode mode, bool cwUplink) =>
        cwUplink && SupportsCwUplinkToggle(mode) ? "CW" : mode.UplinkMode;

    public static string GetEffectiveDownlinkMode(SatelliteTransponderMode mode, bool cwUplink) =>
        cwUplink && SupportsCwUplinkToggle(mode) ? "CW" : mode.DownlinkMode;

    public static (string Uplink, string Downlink) GetEffectiveModes(
        SatelliteTransponderMode mode,
        bool cwUplink) =>
        (GetEffectiveUplinkMode(mode, cwUplink), GetEffectiveDownlinkMode(mode, cwUplink));

    private static bool IsLinearSideband(string mode)
    {
        var upper = mode.Trim().ToUpperInvariant();
        return upper is "USB" or "LSB";
    }

    private static bool IsCw(string mode) =>
        mode.Trim().Equals("CW", StringComparison.OrdinalIgnoreCase);
}
