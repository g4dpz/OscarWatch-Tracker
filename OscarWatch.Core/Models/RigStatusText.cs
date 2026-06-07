namespace OscarWatch.Core.Models;

/// <summary>English rig status text for logs, diagnostics, and debug output.</summary>
public static class RigStatusText
{
    public static string ToEnglish(RigConnectionStatus status)
    {
        switch (status.StatusKind)
        {
            case RigStatusKind.None:
                return status.IsConnected ? "Connected" : "Disconnected";
            case RigStatusKind.Disconnected:
                return "Disconnected";
            case RigStatusKind.Connected:
                return "Connected";
            case RigStatusKind.CatPaused:
                return "CAT paused (manual tuning)";
            case RigStatusKind.Tracking:
                return "Tracking";
            case RigStatusKind.NoComPort:
                return "No COM port selected";
            case RigStatusKind.SelectDualComPorts:
                return "Select COM ports for downlink and uplink radios";
            case RigStatusKind.DualNotConnected:
                return FormatConnectionFailure("Dual radio not connected", status.StatusPort, status.StatusDetail);
            case RigStatusKind.NotConnected:
                var baseMessage = string.IsNullOrWhiteSpace(status.StatusPort)
                    ? "Rig not connected"
                    : $"Rig not connected ({status.StatusPort})";
                return string.IsNullOrWhiteSpace(status.StatusDetail)
                    ? baseMessage
                    : $"{baseMessage}: {status.StatusDetail}";
            default:
                return status.StatusDetail ?? status.StatusKind.ToString();
        }
    }

    private static string FormatConnectionFailure(string baseMessage, string? port, string? detail)
    {
        if (!string.IsNullOrWhiteSpace(port) && string.IsNullOrWhiteSpace(detail))
            return $"{baseMessage} ({port})";

        return string.IsNullOrWhiteSpace(detail)
            ? baseMessage
            : $"{baseMessage}: {detail}";
    }
}
