using OscarWatch.Localization;

namespace OscarWatch.Tests;

public sealed class LocalizationServiceTests
{
    [Fact]
    public void Get_returns_english_by_default()
    {
        using var _ = TestUiCulture.Apply(LocalizationCulture.DefaultLanguage);
        var text = LocalizationService.Instance.Get("Menu.File");
        Assert.Equal("_File", text);
    }

    [Fact]
    public void Get_returns_portuguese_brazil_when_culture_is_pt_BR()
    {
        using var _ = TestUiCulture.Apply(LocalizationCulture.PortugueseBrazilLanguage);
        var text = LocalizationService.Instance.Get("Menu.File");
        Assert.Equal("_Arquivo", text);
    }

    [Fact]
    public void Get_returns_chinese_when_culture_is_zh_CN()
    {
        using var _ = TestUiCulture.Apply(LocalizationCulture.SimplifiedChineseLanguage);
        var text = LocalizationService.Instance.Get("Menu.File");
        Assert.Equal("_文件", text);
    }

    [Fact]
    public void Get_returns_japanese_when_culture_is_ja()
    {
        using var _ = TestUiCulture.Apply(LocalizationCulture.JapaneseLanguage);
        var text = LocalizationService.Instance.Get("Menu.File");
        Assert.Equal("ファイル(_F)", text);
    }

    [Fact]
    public void Get_falls_back_to_english_for_untranslated_key()
    {
        using var _ = TestUiCulture.Apply(LocalizationCulture.JapaneseLanguage);
        var text = LocalizationService.Instance.Get("Settings.Tab.Cloudlog");
        Assert.Equal("Cloudlog", text);
    }

    [Fact]
    public void Get_returns_japanese_status_satellite_count()
    {
        using var _ = TestUiCulture.Apply(LocalizationCulture.JapaneseLanguage);
        var text = LocalizationService.Instance.Get("Status.SatellitesEnabled", "TLE 01:23 前 (cache)", 3);
        Assert.Equal("TLE 01:23 前 (cache) | 3 機の衛星を有効", text);
    }

    [Fact]
    public void Get_returns_japanese_pass_planner_status()
    {
        using var _ = TestUiCulture.Apply(LocalizationCulture.JapaneseLanguage);
        var text = LocalizationService.Instance.Get("Pass.CountPasses", 3, 48);
        Assert.Equal("今後 48 時間で 3 件のパス", text);
    }

    [Fact]
    public void Get_formats_arguments_with_current_culture()
    {
        using var _ = TestUiCulture.Apply(LocalizationCulture.DefaultLanguage);
        var text = LocalizationService.Instance.Get("Main.Pass.AosIn", "ISS", "00:12:34");
        Assert.Equal("ISS AOS in 00:12:34", text);
    }
}
