using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using OscarWatch.Core.Services;
using OscarWatch.ViewModels;

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

        App.Services?.GetService<MainViewModel>()?.DisconnectHardwareForShutdown();

        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
