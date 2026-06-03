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

    private static readonly Uri EmbeddedJapaneseFontUri = new(
        $"avares://OscarWatch/Assets/Fonts/{EmbeddedJapaneseFontFileName}");

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

    public static AppBuilder Configure(AppBuilder builder, string uiLanguage)
    {
        if (HasEmbeddedJapaneseFont())
        {
            var fontsUri = new Uri("avares://OscarWatch/Assets/Fonts", UriKind.Absolute);
            var collectionUri = new Uri(CollectionKey, UriKind.Absolute);
            builder = builder.ConfigureFonts(manager =>
                manager.AddFontCollection(new EmbeddedFontCollection(collectionUri, fontsUri)));
        }

        var isJapanese = string.Equals(
            uiLanguage, LocalizationCulture.JapaneseLanguage, StringComparison.OrdinalIgnoreCase);
        var isChinese = IsSimplifiedChinese(uiLanguage);

        if (isJapanese || isChinese)
        {
            var cjkPrimary = ResolveCjkPrimaryFamily(isJapanese);

            builder = builder.With(new FontManagerOptions
            {
                DefaultFamilyName = $"{cjkPrimary}, fonts:Inter#Inter, $Default",
                FontFallbacks =
                [
                    new FontFallback { FontFamily = new FontFamily(cjkPrimary) },
                    new FontFallback { FontFamily = new FontFamily("fonts:Inter#Inter") }
                ]
            });
        }
        else
        {
            builder = builder.With(new FontManagerOptions
            {
                DefaultFamilyName = "fonts:Inter#Inter, $Default",
                FontFallbacks = BuildCjkFallbacks()
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

    private static FontFallback[] BuildCjkFallbacks()
    {
        if (HasEmbeddedJapaneseFont())
        {
            return
            [
                new FontFallback { FontFamily = new FontFamily(EmbeddedJapaneseFontFamily) },
                new FontFallback { FontFamily = new FontFamily("fonts:Inter#Inter") }
            ];
        }

        return JapaneseSystemFamilies
            .Select(f => new FontFallback { FontFamily = new FontFamily(f) })
            .Append(new FontFallback { FontFamily = new FontFamily("fonts:Inter#Inter") })
            .ToArray();
    }

    private static bool HasEmbeddedJapaneseFont()
    {
        try
        {
            if (AssetLoader.Exists(EmbeddedJapaneseFontUri))
                return true;
        }
        catch
        {
            // AssetLoader not ready in some design-time hosts
        }

        var path = Path.Combine(
            AppContext.BaseDirectory, "Assets", "Fonts", EmbeddedJapaneseFontFileName);
        return File.Exists(path) && new FileInfo(path).Length > 100_000;
    }
}
