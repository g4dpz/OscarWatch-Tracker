using System.Globalization;
using OscarWatch.Localization;

namespace OscarWatch.Tests;

public sealed class LocalizationCultureTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("en")]
    [InlineData("en-GB")]
    public void ResolveCulture_maps_english_aliases_to_en_GB(string? uiLanguage)
    {
        var culture = LocalizationCulture.ResolveCulture(uiLanguage);
        Assert.Equal("en-GB", culture.Name);
    }

    [Theory]
    [InlineData(null, "en-GB")]
    [InlineData("en", "en-GB")]
    [InlineData("en-GB", "en-GB")]
    [InlineData("ja", "ja")]
    [InlineData("es", "es")]
    public void NormalizeLanguageCode_maps_legacy_en_to_en_GB(string? input, string expected)
    {
        Assert.Equal(expected, LocalizationCulture.NormalizeLanguageCode(input));
    }

    [Fact]
    public void ResolveCulture_maps_spanish_to_es()
    {
        var culture = LocalizationCulture.ResolveCulture("es");
        Assert.Equal("es", culture.Name);
    }

    [Fact]
    public void Apply_sets_thread_cultures_to_en_GB_for_english()
    {
        using var _ = TestUiCulture.Apply("en");
        Assert.Equal("en-GB", CultureInfo.CurrentUICulture.Name);
        Assert.Equal("en-GB", CultureInfo.CurrentCulture.Name);
    }

    [Fact]
    public void ReadUiLanguageFromFile_reads_camelCase_uiLanguage_from_settings_json()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{"uiLanguage":"es"}""");

        var language = LocalizationCulture.ReadUiLanguageFromFile(path);

        Assert.Equal("es", language);
    }
}
