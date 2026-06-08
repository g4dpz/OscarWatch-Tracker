using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace OscarWatch.Theme;

/// <summary>
/// Theme-aware brushes that meet contrast targets in light and dark UI.
/// </summary>
public static class AccessibilityThemeResources
{
    public const string ThemeForegroundKey = "ThemeForegroundBrush";
    public const string ThemeSecondaryForegroundKey = "ThemeSecondaryForegroundBrush";
    public const string ThemePlaceholderForegroundKey = "ThemePlaceholderForegroundBrush";
    public const string PassHighlightKey = "PassHighlightBrush";
    public const string PassInProgressBackgroundKey = "PassInProgressBackgroundBrush";
    public const string PassRecordingBackgroundKey = "PassRecordingBackgroundBrush";
    public const string PassImminentBackgroundKey = "PassImminentBackgroundBrush";
    public const string PassImminentBadgeBackgroundKey = "PassImminentBadgeBackgroundBrush";
    public const string PassImminentBadgeForegroundKey = "PassImminentBadgeForegroundBrush";
    public const string PassInProgressBadgeBackgroundKey = "PassInProgressBadgeBackgroundBrush";
    public const string PassInProgressBadgeForegroundKey = "PassInProgressBadgeForegroundBrush";
    public const string HamsAtGridBadgeBackgroundKey = "HamsAtGridBadgeBackgroundBrush";
    public const string PassRecordingBadgeBackgroundKey = "PassRecordingBadgeBackgroundBrush";
    public const string PassRecordingBadgeForegroundKey = "PassRecordingBadgeForegroundBrush";
    public const string StaleTleKey = "StaleTleForegroundBrush";
    public const string SunlightStatusKey = "SunlightStatusBrush";
    public const string EclipseStatusKey = "EclipseStatusBrush";
    public const string GpsOkKey = "GpsOkBrush";
    public const string GpsWarnKey = "GpsWarnBrush";

    public static void Install()
    {
        if (Application.Current is null)
            return;

        Application.Current.ActualThemeVariantChanged += (_, _) => Apply();
        Apply();
    }

    public static void Apply()
    {
        if (Application.Current?.Resources is not { } resources)
            return;

        var isDark = Application.Current.ActualThemeVariant == ThemeVariant.Dark;
        resources[ThemeForegroundKey] = new SolidColorBrush(
            isDark ? Colors.White : Color.Parse("#1A1A1A"));
        resources[ThemeSecondaryForegroundKey] = new SolidColorBrush(
            isDark ? Color.Parse("#C8CDD4") : Color.Parse("#525252"));
        resources[ThemePlaceholderForegroundKey] = new SolidColorBrush(
            isDark ? Color.Parse("#9CA3AF") : Color.Parse("#5C6370"));

        // Fluent defaults are too faint on light backgrounds for labels, hints, and watermarks.
        resources["SystemControlForegroundBaseMediumBrush"] = resources[ThemeSecondaryForegroundKey];
        resources["TextControlPlaceholderForeground"] = resources[ThemePlaceholderForegroundKey];
        resources["TextControlPlaceholderOpacity"] = 1.0;
        resources[PassHighlightKey] = new SolidColorBrush(
            isDark ? Color.Parse("#9EDE6B") : Color.Parse("#2B6E1F"));
        resources[PassInProgressBackgroundKey] = new SolidColorBrush(
            isDark ? Color.Parse("#2A4A24") : Color.Parse("#D4EDCC"));
        resources[PassRecordingBackgroundKey] = new SolidColorBrush(
            isDark ? Color.Parse("#1F3D2E") : Color.Parse("#C8E6C0"));
        resources[PassImminentBackgroundKey] = new SolidColorBrush(
            isDark ? Color.Parse("#4D3D1A") : Color.Parse("#FFF0D4"));
        resources[PassImminentBadgeBackgroundKey] = new SolidColorBrush(
            isDark ? Color.Parse("#C47A1A") : Color.Parse("#E89B1E"));
        resources[PassImminentBadgeForegroundKey] = new SolidColorBrush(
            isDark ? Colors.White : Color.Parse("#1A1A1A"));
        resources[PassInProgressBadgeBackgroundKey] = new SolidColorBrush(
            isDark ? Color.Parse("#3D8B2E") : Color.Parse("#2B6E1F"));
        resources[PassInProgressBadgeForegroundKey] = new SolidColorBrush(Colors.White);
        resources[HamsAtGridBadgeBackgroundKey] = new SolidColorBrush(
            isDark ? Color.Parse("#256322") : Color.Parse("#2B6E1F"));
        resources[PassRecordingBadgeBackgroundKey] = new SolidColorBrush(
            isDark ? Color.Parse("#B91C1C") : Color.Parse("#DC2626"));
        resources[PassRecordingBadgeForegroundKey] = new SolidColorBrush(Colors.White);
        resources[StaleTleKey] = new SolidColorBrush(
            isDark ? Color.Parse("#FFB347") : Color.Parse("#B45309"));
        resources[SunlightStatusKey] = new SolidColorBrush(
            isDark ? Color.Parse("#F5C842") : Color.Parse("#B8860B"));
        resources[EclipseStatusKey] = new SolidColorBrush(
            isDark ? Color.Parse("#9CA3AF") : Color.Parse("#5C6370"));
        resources[GpsOkKey] = resources[PassHighlightKey];
        resources[GpsWarnKey] = resources[PassRecordingBadgeBackgroundKey];
    }
}
