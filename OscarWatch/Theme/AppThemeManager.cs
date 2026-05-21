using Avalonia;
using Avalonia.Styling;
using OscarWatch.Core.Models;

namespace OscarWatch.Theme;

public static class AppThemeManager
{
    public static void Apply(AppThemePreference preference)
    {
        if (Application.Current is null)
            return;

        Application.Current.RequestedThemeVariant = preference switch
        {
            AppThemePreference.Light => ThemeVariant.Light,
            AppThemePreference.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };

        AccessibilityThemeResources.Apply();
    }
}
