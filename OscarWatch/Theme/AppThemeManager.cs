using Avalonia;
using Avalonia.Styling;
using OscarWatch.Core.Models;
using System.Text.Json;

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

    public static AppThemePreference ReadPreferenceFromDisk()
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OscarWatch",
                "settings.json");

            if (!File.Exists(path))
                return AppThemePreference.System;

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("theme", out var themeValue))
                return AppThemePreference.System;

            if (themeValue.ValueKind == JsonValueKind.String)
            {
                var value = themeValue.GetString();
                if (Enum.TryParse<AppThemePreference>(value, ignoreCase: true, out var parsed))
                    return parsed;
            }
            else if (themeValue.ValueKind == JsonValueKind.Number
                     && themeValue.TryGetInt32(out var numeric)
                     && Enum.IsDefined(typeof(AppThemePreference), numeric))
            {
                return (AppThemePreference)numeric;
            }
        }
        catch
        {
            // Fall back to default theme.
        }

        return AppThemePreference.System;
    }
}
