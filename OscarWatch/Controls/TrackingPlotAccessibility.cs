using Avalonia.Automation;
using Avalonia.Controls;
using OscarWatch.Core.Models;

namespace OscarWatch.Controls;

internal static class TrackingPlotAccessibility
{
    public static void UpdateName(
        Control control,
        string plotKind,
        IReadOnlyList<SatelliteTrackState>? states,
        string? focusedNoradId)
    {
        var count = states?.Count ?? 0;
        if (count == 0)
        {
            AutomationProperties.SetName(control, $"{plotKind}. No satellites selected.");
            return;
        }

        if (focusedNoradId is null)
        {
            AutomationProperties.SetName(
                control,
                $"{plotKind}. {count} satellite(s). Use arrow keys to select, Enter to confirm.");
            return;
        }

        var focused = states!.FirstOrDefault(s => s.NoradId == focusedNoradId);
        if (focused is null)
        {
            AutomationProperties.SetName(control, $"{plotKind}. {count} satellite(s).");
            return;
        }

        var detail = focused.LookAngles is { } la
            ? $" Azimuth {la.AzimuthDeg:F1} degrees, elevation {la.ElevationDeg:F1} degrees, altitude {focused.Subpoint.AltitudeKm:F0} kilometers."
            : $" Altitude {focused.Subpoint.AltitudeKm:F0} kilometers, below horizon.";
        AutomationProperties.SetName(
            control,
            $"{plotKind}. Focused {focused.Name}.{detail}");
    }

    public static string? CycleFocusedNoradId(
        IReadOnlyList<SatelliteTrackState>? states,
        string? currentNoradId,
        int direction)
    {
        if (states is not { Count: > 0 })
            return null;

        var index = 0;
        if (currentNoradId is not null)
        {
            for (var i = 0; i < states.Count; i++)
            {
                if (states[i].NoradId == currentNoradId)
                {
                    index = i;
                    break;
                }
            }
        }

        index = (index + direction + states.Count) % states.Count;
        return states[index].NoradId;
    }

    public static bool IsPlotSatelliteVisible(bool soloFocusedSatellite, string? focusedNoradId, string noradId)
    {
        if (!soloFocusedSatellite)
            return true;

        return !string.IsNullOrEmpty(focusedNoradId)
            && string.Equals(focusedNoradId, noradId, StringComparison.Ordinal);
    }
}
