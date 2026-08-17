using OscarWatch.Core.Models;

namespace OscarWatch.Core.Rotator;

/// <summary>
/// Offline walk of polar-plot samples with the live Smart450 command picker,
/// for Pass Visualiser hover tooltips. Seeds from AOS with no prior dial state.
/// </summary>
public static class SmartAzimuthPassPreview
{
    /// <summary>
    /// Fills <see cref="PassPolarPlotSample.CommandAzimuthDeg"/> when Smart450 applies.
    /// Returns true when command azimuths were written.
    /// </summary>
    public static bool TryApply(
        IReadOnlyList<PassPolarPlotSample> samples,
        bool smartAzimuth450,
        double maxAzimuthDeg)
    {
        if (!smartAzimuth450 || maxAzimuthDeg <= 360 || samples.Count == 0)
            return false;

        double? lastCommanded = null;
        for (var i = 0; i < samples.Count; i++)
        {
            double? nextCompass = i + 1 < samples.Count
                ? samples[i + 1].AzimuthDeg
                : null;
            var command = RotatorAzimuthPlanner.ResolveCommandAz(
                lastCommanded,
                samples[i].AzimuthDeg,
                maxAzimuthDeg,
                nextCompass,
                RemainingSamplesCrossNorthEastToWest(samples, i));
            samples[i].CommandAzimuthDeg = command;
            lastCommanded = command;
        }

        return true;
    }

    internal static bool RemainingSamplesCrossNorthEastToWest(
        IReadOnlyList<PassPolarPlotSample> samples,
        int fromIndex)
    {
        for (var i = fromIndex; i < samples.Count - 1; i++)
        {
            if (RotatorAzimuthPlanner.IndicatesEastToWestNorthCrossing(
                    samples[i].AzimuthDeg, samples[i + 1].AzimuthDeg))
                return true;
        }

        return false;
    }

    public static bool UsesExtendedBand(IReadOnlyList<PassPolarPlotSample> samples)
    {
        for (var i = 0; i < samples.Count; i++)
        {
            if (samples[i].CommandAzimuthDeg is > 360)
                return true;
        }

        return false;
    }
}
