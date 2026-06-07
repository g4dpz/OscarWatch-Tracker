using OscarWatch.Core.Models;

namespace OscarWatch.Core.Hardware;

public static class DualRadioConfigHelper
{
    public const string MissingDownlinkCode = "downlink";
    public const string MissingUplinkCode = "uplink";
    public const string MissingBothCode = "both";

    public static bool IsIncomplete(RigSettings rig) =>
        rig.Enabled && rig.DualRadioEnabled && !rig.IsDualRadioConfigured;

    public static string IncompleteCode(RigSettings rig)
    {
        if (!IsIncomplete(rig))
            return "";

        if (!rig.Downlink.IsConfigured && !rig.Uplink.IsConfigured)
            return MissingBothCode;

        return !rig.Downlink.IsConfigured ? MissingDownlinkCode : MissingUplinkCode;
    }

    public static bool TryDescribeIncomplete(RigSettings rig, out string code)
    {
        code = IncompleteCode(rig);
        return code.Length > 0;
    }
}
