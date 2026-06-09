using Avalonia.Automation;
using Avalonia.Controls;
using OscarWatch.Core.Models;
using OscarWatch.Localization;

namespace OscarWatch.Controls;

internal static class TrackingPlotAccessibility
{
    private static ILocalizationService L => LocalizationService.Instance;

    public static void UpdateName(
        Control control,
        string plotKind,
        IReadOnlyList<SatelliteTrackState>? states,
        string? focusedNoradId)
    {
        var count = states?.Count ?? 0;
        if (count == 0)
        {
            AutomationProperties.SetName(control, L.Get("A11y.Plot.NoSatellites", plotKind));
            return;
        }

        if (focusedNoradId is null)
        {
            AutomationProperties.SetName(
                control,
                L.Get("A11y.Plot.SelectWithArrows", plotKind, count));
            return;
        }

        var focused = states!.FirstOrDefault(s => s.NoradId == focusedNoradId);
        if (focused is null)
        {
            AutomationProperties.SetName(control, L.Get("A11y.Plot.SatelliteCount", plotKind, count));
            return;
        }

        var name = focused.LookAngles is { } la
            ? L.Get(
                "A11y.Plot.FocusedWithLookAngles",
                plotKind,
                focused.Name,
                la.AzimuthDeg,
                la.ElevationDeg,
                focused.Subpoint.AltitudeKm)
            : L.Get(
                "A11y.Plot.FocusedBelowHorizon",
                plotKind,
                focused.Name,
                focused.Subpoint.AltitudeKm);
        AutomationProperties.SetName(control, name);
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
