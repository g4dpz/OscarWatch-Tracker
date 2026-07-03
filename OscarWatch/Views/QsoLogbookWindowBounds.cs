using Avalonia;
using Avalonia.Controls;
using OscarWatch.Core.Models;

namespace OscarWatch.Views;

internal static class QsoLogbookWindowBounds
{
    public static void Apply(Window window, QsoLogbookSettings? settings)
    {
        if (QsoLogbookWindowDefaults.TryGetSavedSize(settings, out var width, out var height))
        {
            window.Width = width;
            window.Height = height;
        }

        if (!QsoLogbookWindowDefaults.TryGetSavedPosition(settings, out var x, out var y))
            return;

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Position = ClampToVisibleArea(window, new PixelPoint(x, y));
    }

    public static void Capture(Window window, QsoLogbookSettings settings)
    {
        settings.WindowWidth = Math.Max(QsoLogbookWindowDefaults.MinWidth, (int)Math.Round(window.Width));
        settings.WindowHeight = Math.Max(QsoLogbookWindowDefaults.MinHeight, (int)Math.Round(window.Height));
        settings.WindowX = window.Position.X;
        settings.WindowY = window.Position.Y;
    }

    internal static PixelPoint ClampToVisibleArea(Window window, PixelPoint position)
    {
        var screens = window.Screens;
        if (screens is null)
            return position;

        var screen = screens.ScreenFromPoint(position) ?? screens.Primary;
        if (screen is null)
            return position;

        var area = screen.WorkingArea;
        var width = (int)Math.Max(window.Width, QsoLogbookWindowDefaults.MinWidth);
        var height = (int)Math.Max(window.Height, QsoLogbookWindowDefaults.MinHeight);
        var maxX = Math.Max(area.X, area.Right - width);
        var maxY = Math.Max(area.Y, area.Bottom - height);
        var x = Math.Clamp(position.X, area.X, maxX);
        var y = Math.Clamp(position.Y, area.Y, maxY);
        return new PixelPoint(x, y);
    }
}
