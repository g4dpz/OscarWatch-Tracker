using OscarWatch.Core.Models;

namespace OscarWatch.Localization;

public static class RigStatusLocalizer
{
    public static string Localize(ILocalizationService localization, RigConnectionStatus status)
    {
        if (status.StatusKind == RigStatusKind.None)
            return status.IsConnected
                ? localization.Get("Rig.Connected")
                : localization.Get("Rig.Disconnected");

        switch (status.StatusKind)
        {
            case RigStatusKind.Disconnected:
                return localization.Get("Rig.Disconnected");
            case RigStatusKind.Connected:
                return localization.Get("Rig.Connected");
            case RigStatusKind.CatPaused:
                return localization.Get("Rig.CatPaused");
            case RigStatusKind.Tracking:
                return localization.Get("Rig.Tracking");
            case RigStatusKind.NoComPort:
                return localization.Get("Rig.NoComPort");
            case RigStatusKind.SelectDualComPorts:
                return localization.Get("Rig.SelectDualComPorts");
            case RigStatusKind.DualNotConnected:
                return string.IsNullOrWhiteSpace(status.StatusDetail)
                    ? localization.Get("Rig.DualNotConnected")
                    : localization.Get("Rig.DualNotConnectedDetail", status.StatusDetail);
            case RigStatusKind.NotConnected:
                if (!string.IsNullOrWhiteSpace(status.StatusPort) && !string.IsNullOrWhiteSpace(status.StatusDetail))
                    return localization.Get("Rig.NotConnectedPortDetail", status.StatusPort, status.StatusDetail);
                if (!string.IsNullOrWhiteSpace(status.StatusPort))
                    return localization.Get("Rig.NotConnectedPort", status.StatusPort);
                if (!string.IsNullOrWhiteSpace(status.StatusDetail))
                    return localization.Get("Rig.NotConnectedDetail", status.StatusDetail);
                return localization.Get("Rig.NotConnected");
            default:
                return status.StatusDetail ?? RigStatusText.ToEnglish(status);
        }
    }
}
