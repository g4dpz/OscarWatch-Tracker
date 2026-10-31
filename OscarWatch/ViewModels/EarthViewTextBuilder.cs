using System.Globalization;
using OscarWatch.Core.Geo;
using OscarWatch.Localization;

namespace OscarWatch.ViewModels;

/// <summary>
/// Builds the readable text for the Earth View from a frame, using localised strings. The text carries
/// everything the picture shows (whether the station is inside the footprint, elevation, sunlit state),
/// so nothing depends on colour or on seeing the globe.
/// </summary>
internal static class EarthViewTextBuilder
{
    public const string KeyEmptyNoSatellite = "EarthView.Empty.NoSatellite";
    public const string KeyEmptyNoPosition = "EarthView.Empty.NoPosition";
    public const string KeyStatusSatellite = "EarthView.Status.Satellite";
    public const string KeyStatusTime = "EarthView.Status.Time";
    public const string KeyStatusLookAngles = "EarthView.Status.LookAngles";
    public const string KeyStatusBelowHorizon = "EarthView.Status.BelowHorizon";
    public const string KeyStatusAboveHorizon = "EarthView.Status.AboveHorizon";
    public const string KeyStatusInsideMinElevation = "EarthView.Status.InsideMinElevation";
    public const string KeyStatusOutsideMinElevation = "EarthView.Status.OutsideMinElevation";
    public const string KeyStatusSunlit = "EarthView.Status.Sunlit";
    public const string KeyStatusEclipse = "EarthView.Status.Eclipse";
    public const string KeyAccessibleName = "EarthView.Accessible.Name";
    public const string KeyAccessibleUsable = "EarthView.Accessible.Summary.Usable";
    public const string KeyAccessibleAboveHorizon = "EarthView.Accessible.Summary.AboveHorizon";
    public const string KeyAccessibleBelowHorizon = "EarthView.Accessible.Summary.BelowHorizon";
    public const string KeyAccessibleNoAngles = "EarthView.Accessible.Summary.NoAngles";

    /// <summary>One line per fact, separated by new lines, for the text panel beside the globe.</summary>
    public static string Status(ILocalizationService l, EarthViewFrame frame)
    {
        var lines = new List<string>
        {
            l.Get(KeyStatusSatellite, frame.SatelliteName, frame.Camera.AltitudeKm),
            l.Get(KeyStatusTime, frame.DisplayUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
        };

        if (frame.LookFromHome is { } look)
            lines.Add(l.Get(KeyStatusLookAngles, look.AzimuthDeg, look.ElevationDeg, look.RangeKm));

        switch (frame.HomeSituation)
        {
            case EarthViewSituation.BelowHorizon:
                lines.Add(l.Get(KeyStatusBelowHorizon));
                break;
            case EarthViewSituation.AboveHorizon:
                lines.Add(l.Get(KeyStatusAboveHorizon));
                lines.Add(l.Get(KeyStatusOutsideMinElevation, frame.MinimumElevationDeg));
                break;
            case EarthViewSituation.Usable:
                lines.Add(l.Get(KeyStatusAboveHorizon));
                lines.Add(l.Get(KeyStatusInsideMinElevation, frame.MinimumElevationDeg));
                break;
        }

        lines.Add(l.Get(frame.IsSunlit ? KeyStatusSunlit : KeyStatusEclipse));
        return string.Join("\n", lines);
    }

    /// <summary>The control's accessible name: short, and names the satellite.</summary>
    public static string AccessibleName(ILocalizationService l, EarthViewFrame? frame) =>
        l.Get(KeyAccessibleName, frame?.SatelliteName ?? "");

    /// <summary>
    /// The control's accessible help text: the satellite, where it is in the sky, and whether the
    /// station is inside the coverage. Angles are rounded to whole degrees for speech.
    /// </summary>
    public static string AccessibleSummary(ILocalizationService l, EarthViewFrame frame)
    {
        if (frame.HomeSituation == EarthViewSituation.BelowHorizon)
            return l.Get(KeyAccessibleBelowHorizon, frame.SatelliteName);

        if (frame.LookFromHome is not { } look)
            return l.Get(KeyAccessibleNoAngles, frame.SatelliteName);

        var key = frame.HomeSituation == EarthViewSituation.Usable ? KeyAccessibleUsable : KeyAccessibleAboveHorizon;
        return l.Get(
            key,
            frame.SatelliteName,
            Math.Round(look.ElevationDeg),
            Math.Round(look.AzimuthDeg),
            frame.MinimumElevationDeg);
    }
}
