using Avalonia.Controls;
using OscarWatch.Core.Models;

namespace OscarWatch.Views;

internal static class QsoLogbookHistoryGridLayout
{
    public static void Apply(DataGrid grid, QsoLogbookSettings? settings)
    {
        var columns = grid.Columns;
        for (var i = 0; i < QsoLogbookHistoryColumns.All.Count && i < columns.Count; i++)
        {
            var key = QsoLogbookHistoryColumns.All[i];
            var column = columns[i];
            if (QsoLogbookHistoryColumns.TryGetSavedPixelWidth(settings, key, out var pixels))
                column.Width = new DataGridLength(pixels);
            else if (QsoLogbookHistoryColumns.UsesStarWidthByDefault(key))
                column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
            else
                column.Width = new DataGridLength(QsoLogbookHistoryColumns.DefaultPixelWidth(key));
        }
    }

    public static void Capture(DataGrid grid, QsoLogbookSettings settings)
    {
        settings.HistoryColumnWidthsPx ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        settings.HistoryColumnWidthsPx.Clear();

        var columns = grid.Columns;
        for (var i = 0; i < QsoLogbookHistoryColumns.All.Count && i < columns.Count; i++)
        {
            var key = QsoLogbookHistoryColumns.All[i];
        var width = columns[i].Width;
        if (width.UnitType == DataGridLengthUnitType.Star)
        {
            if (!QsoLogbookHistoryColumns.UsesStarWidthByDefault(key))
                settings.HistoryColumnWidthsPx[key] = QsoLogbookHistoryColumns.DefaultPixelWidth(key);
            continue;
        }

        settings.HistoryColumnWidthsPx[key] = Math.Max(
            QsoLogbookHistoryColumns.MinimumPixelWidth,
            (int)Math.Round(width.Value));
        }
    }
}
