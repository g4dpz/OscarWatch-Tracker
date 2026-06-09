using System.Globalization;
using System.Text.Json;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Localization;

/// <summary>Applies UI culture from settings before windows are created.</summary>
public static class LocalizationCulture
{
    /// <summary>Default UI language: British English (<c>en-GB</c>). Legacy <c>en</c> is accepted as an alias.</summary>
    public const string DefaultLanguage = "en-GB";
    public const string JapaneseLanguage = "ja";
    public const string PortugueseBrazilLanguage = "pt-BR";
    public const string SimplifiedChineseLanguage = "zh-CN";

    public static void ApplyFromSettings(ISettingsService settings) =>
        Apply(NormalizeLanguageCode(settings.Current.UiLanguage));

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

        if (string.Equals(uiLanguage, SimplifiedChineseLanguage, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uiLanguage, "zh-Hans", StringComparison.OrdinalIgnoreCase))
            return CultureInfo.GetCultureInfo(SimplifiedChineseLanguage);

        if (string.IsNullOrWhiteSpace(uiLanguage)
            || string.Equals(uiLanguage, "en", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uiLanguage, DefaultLanguage, StringComparison.OrdinalIgnoreCase))
            return CultureInfo.GetCultureInfo(DefaultLanguage);

        return CultureInfo.GetCultureInfo(DefaultLanguage);
    }

    /// <summary>Maps legacy <c>en</c> and empty values to <see cref="DefaultLanguage"/>.</summary>
    public static string NormalizeLanguageCode(string? uiLanguage)
    {
        if (string.IsNullOrWhiteSpace(uiLanguage))
            return DefaultLanguage;

        var trimmed = uiLanguage.Trim();
        return string.Equals(trimmed, "en", StringComparison.OrdinalIgnoreCase)
            ? DefaultLanguage
            : trimmed;
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
                    return NormalizeLanguageCode(value);
            }
        }
        catch
        {
            // use default
        }

        return DefaultLanguage;
    }
}
