using System.Globalization;
using System.Text.Json;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Localization;

/// <summary>Applies UI culture from settings before windows are created.</summary>
public static class LocalizationCulture
{
    public const string DefaultLanguage = "en";
    public const string JapaneseLanguage = "ja";
    public const string PortugueseBrazilLanguage = "pt-BR";

    public static void ApplyFromSettings(ISettingsService settings) =>
        Apply(settings.Current.UiLanguage);

    public static void Apply(string? uiLanguage)
    {
        var culture = ResolveCulture(uiLanguage);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
    }

    public static CultureInfo ResolveCulture(string? uiLanguage)
    {
        if (string.Equals(uiLanguage, JapaneseLanguage, StringComparison.OrdinalIgnoreCase))
            return CultureInfo.GetCultureInfo("ja");

        if (string.Equals(uiLanguage, PortugueseBrazilLanguage, StringComparison.OrdinalIgnoreCase))
            return CultureInfo.GetCultureInfo(PortugueseBrazilLanguage);

        return CultureInfo.GetCultureInfo(DefaultLanguage);
    }

    /// <summary>Reads <see cref="AppSettings.UiLanguage"/> before DI is available (fonts in Program).</summary>
    public static string ReadUiLanguageFromDisk()
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OscarWatch",
                "settings.json");

            if (!File.Exists(path))
                return DefaultLanguage;

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty(nameof(AppSettings.UiLanguage), out var lang)
                && lang.ValueKind == JsonValueKind.String)
            {
                var value = lang.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
        }
        catch
        {
            // use default
        }

        return DefaultLanguage;
    }
}
