using System.Globalization;
using OscarWatch.Localization;

namespace OscarWatch.Tests;

public sealed class LocalizationServiceTests
{
    [Fact]
    public void Get_returns_english_by_default()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            LocalizationCulture.Apply(LocalizationCulture.DefaultLanguage);
            var text = LocalizationService.Instance.Get("Menu.File");
            Assert.Equal("_File", text);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Get_returns_portuguese_brazil_when_culture_is_pt_BR()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            LocalizationCulture.Apply(LocalizationCulture.PortugueseBrazilLanguage);
            var text = LocalizationService.Instance.Get("Menu.File");
            Assert.Equal("_Arquivo", text);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Get_returns_japanese_when_culture_is_ja()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            LocalizationCulture.Apply(LocalizationCulture.JapaneseLanguage);
            var text = LocalizationService.Instance.Get("Menu.File");
            Assert.Equal("ファイル(_F)", text);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Get_falls_back_to_english_for_untranslated_key()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            LocalizationCulture.Apply(LocalizationCulture.JapaneseLanguage);
            var text = LocalizationService.Instance.Get("Settings.Tab.Cloudlog");
            Assert.Equal("Cloudlog", text);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Get_returns_japanese_status_satellite_count()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            LocalizationCulture.Apply(LocalizationCulture.JapaneseLanguage);
            var text = LocalizationService.Instance.Get("Status.SatellitesEnabled", "TLE 01:23 前 (cache)", 3);
            Assert.Equal("TLE 01:23 前 (cache) | 3 機の衛星を有効", text);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Get_returns_japanese_pass_planner_status()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            LocalizationCulture.Apply(LocalizationCulture.JapaneseLanguage);
            var text = LocalizationService.Instance.Get("Pass.CountPasses", 3, 48);
            Assert.Equal("今後 48 時間で 3 件のパス", text);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Get_formats_arguments_with_current_culture()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            LocalizationCulture.Apply(LocalizationCulture.DefaultLanguage);
            var text = LocalizationService.Instance.Get("Main.Pass.AosIn", "ISS", "00:12:34");
            Assert.Equal("ISS AOS in 00:12:34", text);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
