using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using OscarWatch.Core.Services;

namespace OscarWatch.Services;

public static class AppRestart
{
    public static void Request()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe))
            return;

        try
        {
            var settings = App.Services?.GetService<ISettingsService>();
            settings?.FlushAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // proceed with restart anyway
        }

        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
