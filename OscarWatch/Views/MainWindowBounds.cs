using Avalonia;
using Avalonia.Controls;
using OscarWatch.Core.Models;

namespace OscarWatch.Views;

internal static class MainWindowBounds
{
    public static void Apply(Window window, AppSettings? settings)
    {
        if (MainWindowLayoutDefaults.TryGetSavedSize(settings, out var width, out var height))
        {
            window.Width = width;
            window.Height = height;
        }

        if (MainWindowLayoutDefaults.TryGetSavedPosition(settings, out var x, out var y))
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Position = QsoLogbookWindowBounds.ClampToVisibleArea(window, new PixelPoint(x, y));
        }

        if (settings?.MainWindowMaximized == true)
            window.WindowState = WindowState.Maximized;
    }

    public static void Capture(Window window, AppSettings settings)
    {
        if (window.WindowState == WindowState.Maximized)
        {
            settings.MainWindowMaximized = true;
            return;
        }

        settings.MainWindowMaximized = false;
        if (window.WindowState == WindowState.Minimized)
            return;

        settings.MainWindowWidth = Math.Max(MainWindowLayoutDefaults.MinWidth, (int)Math.Round(window.Width));
        settings.MainWindowHeight = Math.Max(MainWindowLayoutDefaults.MinHeight, (int)Math.Round(window.Height));
        settings.MainWindowX = window.Position.X;
        settings.MainWindowY = window.Position.Y;
    }
}
