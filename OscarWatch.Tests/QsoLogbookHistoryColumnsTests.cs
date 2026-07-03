using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

public sealed class QsoLogbookHistoryColumnsTests
{
    [Fact]
    public void TryGetSavedPixelWidth_rejects_missing_and_too_narrow_values()
    {
        var settings = new QsoLogbookSettings
        {
            HistoryColumnWidthsPx =
            {
                [QsoLogbookHistoryColumns.Call] = 120,
                [QsoLogbookHistoryColumns.Grid] = 20
            }
        };

        Assert.True(QsoLogbookHistoryColumns.TryGetSavedPixelWidth(settings, QsoLogbookHistoryColumns.Call, out var callWidth));
        Assert.Equal(120, callWidth);

        Assert.False(QsoLogbookHistoryColumns.TryGetSavedPixelWidth(settings, QsoLogbookHistoryColumns.Grid, out _));
        Assert.False(QsoLogbookHistoryColumns.TryGetSavedPixelWidth(settings, QsoLogbookHistoryColumns.Mode, out _));
    }

    [Fact]
    public void TryGetSavedSize_rejects_invalid_dimensions()
    {
        var settings = new QsoLogbookSettings { WindowWidth = 700, WindowHeight = 640 };
        Assert.False(QsoLogbookWindowDefaults.TryGetSavedSize(settings, out _, out _));

        settings.WindowWidth = 980;
        settings.WindowHeight = 400;
        Assert.False(QsoLogbookWindowDefaults.TryGetSavedSize(settings, out _, out _));

        settings.WindowHeight = 640;
        Assert.True(QsoLogbookWindowDefaults.TryGetSavedSize(settings, out var width, out var height));
        Assert.Equal(980, width);
        Assert.Equal(640, height);
    }

    [Fact]
    public void SettingsService_round_trips_logbook_window_bounds()
    {
        using var service = new SettingsService(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "settings.json"));
        service.Current.QsoLogbook.WindowWidth = 1024;
        service.Current.QsoLogbook.WindowHeight = 720;
        service.Current.QsoLogbook.WindowX = 120;
        service.Current.QsoLogbook.WindowY = 80;

        var json = service.SerializeCurrent();
        Assert.True(SettingsService.TryParse(json, out var parsed, out var error));

        Assert.Null(error);
        Assert.Equal(1024, parsed.QsoLogbook.WindowWidth);
        Assert.Equal(720, parsed.QsoLogbook.WindowHeight);
        Assert.Equal(120, parsed.QsoLogbook.WindowX);
        Assert.Equal(80, parsed.QsoLogbook.WindowY);
    }

    [Fact]
    public void SettingsService_round_trips_logbook_column_widths()
    {
        using var service = new SettingsService(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "settings.json"));
        service.Current.QsoLogbook.HistoryColumnWidthsPx[QsoLogbookHistoryColumns.DateTime] = 180;
        service.Current.QsoLogbook.HistoryColumnWidthsPx[QsoLogbookHistoryColumns.Comment] = 240;

        var json = service.SerializeCurrent();
        Assert.True(SettingsService.TryParse(json, out var parsed, out var error));

        Assert.Null(error);
        Assert.Equal(180, parsed.QsoLogbook.HistoryColumnWidthsPx[QsoLogbookHistoryColumns.DateTime]);
        Assert.Equal(240, parsed.QsoLogbook.HistoryColumnWidthsPx[QsoLogbookHistoryColumns.Comment]);
    }
}
