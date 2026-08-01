using Avalonia;
using Avalonia.Controls;
using OscarWatch.Core.Models;

namespace OscarWatch.Views;

internal static class TimelineDetachedWindowBounds
{
    public static void Apply(Window window, AppSettings settings)
    {
        if (TimelineDetachedWindowDefaults.TryGetSavedSize(settings, out var width, out var height))
        {
            window.Width = width;
            window.Height = height;
        }

        if (!TimelineDetachedWindowDefaults.TryGetSavedPosition(settings, out var x, out var y))
            return;

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Position = ClampToVisibleArea(window, new PixelPoint(x, y));
    }

    public static void Capture(Window window, AppSettings settings)
    {
        settings.TimelineDetachedWindowWidth =
            Math.Max(TimelineDetachedWindowDefaults.MinWidth, (int)Math.Round(window.Width));
        settings.TimelineDetachedWindowHeight =
            Math.Max(TimelineDetachedWindowDefaults.MinHeight, (int)Math.Round(window.Height));
        settings.TimelineDetachedWindowX = window.Position.X;
        settings.TimelineDetachedWindowY = window.Position.Y;
    }

    public static PixelPoint ClampToVisibleArea(Window window, PixelPoint position)
    {
        var screens = window.Screens;
        if (screens is null)
            return position;

        var screen = screens.ScreenFromPoint(position) ?? screens.Primary;
        if (screen is null)
            return position;

        var area = screen.WorkingArea;
        var width = (int)Math.Max(window.Width, TimelineDetachedWindowDefaults.MinWidth);
        var height = (int)Math.Max(window.Height, TimelineDetachedWindowDefaults.MinHeight);
        var maxX = Math.Max(area.X, area.Right - width);
        var maxY = Math.Max(area.Y, area.Bottom - height);
        var x = Math.Clamp(position.X, area.X, maxX);
        var y = Math.Clamp(position.Y, area.Y, maxY);
        return new PixelPoint(x, y);
    }
}
