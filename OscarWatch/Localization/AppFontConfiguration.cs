using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Avalonia.Platform;

namespace OscarWatch.Localization;

public static class AppFontConfiguration
{
    public const string CollectionKey = "fonts:OscarWatch";
    public const string EmbeddedJapaneseFontFileName = "NotoSansCJKjp-Regular.otf";
    /// <summary>Internal family name from the OTF metadata (not the file name).</summary>
    public const string EmbeddedJapaneseFontFamily = "fonts:OscarWatch#Noto Sans CJK JP";
    public const string EmbeddedThaiFontFileName = "NotoSansThai-Regular.ttf";
    /// <summary>Internal family name from the TTF metadata (not the file name).</summary>
    public const string EmbeddedThaiFontFamily = "fonts:OscarWatch#Noto Sans Thai";

    private static readonly Uri EmbeddedJapaneseFontUri = new(
        $"avares://OscarWatch/Assets/Fonts/{EmbeddedJapaneseFontFileName}");

    private static readonly Uri EmbeddedThaiFontUri = new(
        $"avares://OscarWatch/Assets/Fonts/{EmbeddedThaiFontFileName}");

    private static readonly string[] JapaneseSystemFamilies =
    [
        "Yu Gothic UI",
        "Meiryo UI",
        "Hiragino Sans",
        "Hiragino Kaku Gothic ProN",
        "Noto Sans CJK JP",
        "Noto Sans JP"
    ];

    private static readonly string[] ChineseSystemFamilies =
    [
        "Microsoft YaHei UI",
        "Microsoft YaHei",
        "SimHei",
        "PingFang SC",
        "Noto Sans CJK SC",
        "Noto Sans SC"
    ];

    private static readonly string[] ThaiSystemFamilies =
    [
        "Leelawadee UI",
        "Leelawadee",
        "Tahoma",
        "Thonburi",
        "Noto Sans Thai"
    ];

    public static AppBuilder Configure(AppBuilder builder, string uiLanguage)
    {
        if (HasEmbeddedAppFonts())
        {
            var fontsUri = new Uri("avares://OscarWatch/Assets/Fonts", UriKind.Absolute);
            var collectionUri = new Uri(CollectionKey, UriKind.Absolute);
            builder = builder.ConfigureFonts(manager =>
                manager.AddFontCollection(new EmbeddedFontCollection(collectionUri, fontsUri)));
        }

        var isJapanese = string.Equals(
            uiLanguage, LocalizationCulture.JapaneseLanguage, StringComparison.OrdinalIgnoreCase);
        var isChinese = IsSimplifiedChinese(uiLanguage);
        var isThai = string.Equals(
            uiLanguage, LocalizationCulture.ThaiLanguage, StringComparison.OrdinalIgnoreCase);

        if (isJapanese || isChinese)
        {
            var cjkPrimary = ResolveCjkPrimaryFamily(isJapanese);

            builder = builder.With(new FontManagerOptions
            {
                DefaultFamilyName = $"{cjkPrimary}, fonts:Inter#Inter, $Default",
                FontFallbacks = BuildScriptFallbacks(cjkPrimary)
            });
        }
        else if (isThai)
        {
            var thaiPrimary = ResolveThaiPrimaryFamily();
            builder = builder.With(new FontManagerOptions
            {
                DefaultFamilyName = $"{thaiPrimary}, fonts:Inter#Inter, $Default",
                FontFallbacks = BuildScriptFallbacks(thaiPrimary)
            });
        }
        else
        {
            builder = builder.With(new FontManagerOptions
            {
                DefaultFamilyName = "fonts:Inter#Inter, $Default",
                FontFallbacks = BuildScriptFallbacks(null)
            });
        }

        return builder;
    }

    private static bool IsSimplifiedChinese(string? uiLanguage) =>
        string.Equals(uiLanguage, LocalizationCulture.SimplifiedChineseLanguage, StringComparison.OrdinalIgnoreCase)
        || string.Equals(uiLanguage, "zh-Hans", StringComparison.OrdinalIgnoreCase);

    private static string ResolveCjkPrimaryFamily(bool japanese)
    {
        if (japanese)
        {
            return HasEmbeddedJapaneseFont()
                ? EmbeddedJapaneseFontFamily
                : string.Join(", ", JapaneseSystemFamilies);
        }

        var chinese = string.Join(", ", ChineseSystemFamilies);
        return HasEmbeddedJapaneseFont()
            ? $"{chinese}, {EmbeddedJapaneseFontFamily}"
            : chinese;
    }

    private static string ResolveThaiPrimaryFamily()
    {
        var system = string.Join(", ", ThaiSystemFamilies);
        return HasEmbeddedThaiFont()
            ? $"{EmbeddedThaiFontFamily}, {system}"
            : system;
    }

    private static FontFallback[] BuildScriptFallbacks(string? primaryFamily)
    {
        var fallbacks = new List<FontFallback>();
        if (!string.IsNullOrEmpty(primaryFamily))
            fallbacks.Add(new FontFallback { FontFamily = new FontFamily(primaryFamily) });

        if (HasEmbeddedThaiFont()
            && !string.Equals(primaryFamily, EmbeddedThaiFontFamily, StringComparison.Ordinal))
        {
            fallbacks.Add(new FontFallback { FontFamily = new FontFamily(EmbeddedThaiFontFamily) });
        }

        if (HasEmbeddedJapaneseFont()
            && primaryFamily?.Contains(EmbeddedJapaneseFontFamily, StringComparison.Ordinal) != true)
        {
            fallbacks.Add(new FontFallback { FontFamily = new FontFamily(EmbeddedJapaneseFontFamily) });
        }

        fallbacks.Add(new FontFallback { FontFamily = new FontFamily("fonts:Inter#Inter") });
        return fallbacks.ToArray();
    }

    private static bool HasEmbeddedAppFonts() =>
        HasEmbeddedJapaneseFont() || HasEmbeddedThaiFont();

    private static bool HasEmbeddedJapaneseFont() =>
        EmbeddedFontExists(EmbeddedJapaneseFontUri, EmbeddedJapaneseFontFileName, 100_000);

    private static bool HasEmbeddedThaiFont() =>
        EmbeddedFontExists(EmbeddedThaiFontUri, EmbeddedThaiFontFileName, 20_000);

    private static bool EmbeddedFontExists(Uri assetUri, string fileName, int minLength)
    {
        try
        {
            if (AssetLoader.Exists(assetUri))
                return true;
        }
        catch
        {
            // AssetLoader not ready in some design-time hosts
        }

        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", fileName);
        return File.Exists(path) && new FileInfo(path).Length > minLength;
    }
}
