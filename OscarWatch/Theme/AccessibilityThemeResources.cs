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
    public const string PassHighlightKey = "PassHighlightBrush";
    public const string PassInProgressBackgroundKey = "PassInProgressBackgroundBrush";
    public const string PassImminentBackgroundKey = "PassImminentBackgroundBrush";
    public const string PassImminentBadgeBackgroundKey = "PassImminentBadgeBackgroundBrush";
    public const string PassImminentBadgeForegroundKey = "PassImminentBadgeForegroundBrush";
    public const string PassInProgressBadgeBackgroundKey = "PassInProgressBadgeBackgroundBrush";
    public const string PassInProgressBadgeForegroundKey = "PassInProgressBadgeForegroundBrush";
    public const string PassRecordingBadgeBackgroundKey = "PassRecordingBadgeBackgroundBrush";
    public const string PassRecordingBadgeForegroundKey = "PassRecordingBadgeForegroundBrush";
    public const string StaleTleKey = "StaleTleForegroundBrush";
    public const string SunlightStatusKey = "SunlightStatusBrush";
    public const string EclipseStatusKey = "EclipseStatusBrush";

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
        resources[PassHighlightKey] = new SolidColorBrush(
            isDark ? Color.Parse("#9EDE6B") : Color.Parse("#2B6E1F"));
        resources[PassInProgressBackgroundKey] = new SolidColorBrush(
            isDark ? Color.Parse("#2A4A24") : Color.Parse("#D4EDCC"));
        resources[PassImminentBackgroundKey] = new SolidColorBrush(
            isDark ? Color.Parse("#4D3D1A") : Color.Parse("#FFF0D4"));
        resources[PassImminentBadgeBackgroundKey] = new SolidColorBrush(
            isDark ? Color.Parse("#C47A1A") : Color.Parse("#E89B1E"));
        resources[PassImminentBadgeForegroundKey] = new SolidColorBrush(
            isDark ? Colors.White : Color.Parse("#1A1A1A"));
        resources[PassInProgressBadgeBackgroundKey] = new SolidColorBrush(
            isDark ? Color.Parse("#3D8B2E") : Color.Parse("#2B6E1F"));
        resources[PassInProgressBadgeForegroundKey] = new SolidColorBrush(Colors.White);
        resources[PassRecordingBadgeBackgroundKey] = new SolidColorBrush(
            isDark ? Color.Parse("#B91C1C") : Color.Parse("#DC2626"));
        resources[PassRecordingBadgeForegroundKey] = new SolidColorBrush(Colors.White);
        resources[StaleTleKey] = new SolidColorBrush(
            isDark ? Color.Parse("#FFB347") : Color.Parse("#B45309"));
        resources[SunlightStatusKey] = new SolidColorBrush(
            isDark ? Color.Parse("#F5C842") : Color.Parse("#B8860B"));
        resources[EclipseStatusKey] = new SolidColorBrush(
            isDark ? Color.Parse("#9CA3AF") : Color.Parse("#5C6370"));
    }
}
