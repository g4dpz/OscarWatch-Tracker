using OscarWatch.Core.Models;

namespace OscarWatch.Core.Hardware;

public static class GpsStatusHelper
{
    public static bool ShowGpsIndicator(GpsSettings settings) => settings.Enabled;

    public static bool HasFix(GpsConnectionStatus status) => status.HasFix;

    public static bool ShowGpsTimeIndicator(GpsSettings settings) =>
        settings.Enabled && settings.UseGpsTimeForTracking;

    public static bool IsGpsTimeActive(GpsSettings settings, DateTime? trackingUtc) =>
        ShowGpsTimeIndicator(settings) && trackingUtc is not null;

    public static string? GridSquareForStatus(GpsSettings settings, string? gridSquare) =>
        settings.Enabled && settings.AutoUpdateStation && !string.IsNullOrWhiteSpace(gridSquare)
            ? gridSquare.Trim().ToUpperInvariant()
            : null;
}
