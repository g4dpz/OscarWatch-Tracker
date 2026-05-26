using Avalonia;
using OscarWatch.Diagnostics;
using Serilog;

namespace OscarWatch;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppLogging.Configure();
        AppLogging.RegisterGlobalHandlers();

        IDisposable? singleInstance = null;
        if (!AppSingleInstance.AllowsMultipleInstances(args)
            && !AppSingleInstance.TryBecomePrimaryInstance(out singleInstance))
        {
            if (AppSingleInstance.NotifyPrimaryInstance())
                Log.Information("Another OscarWatch instance is already running; activated existing window.");
            else
                Log.Warning("Another OscarWatch instance appears to be running but could not be activated.");

            AppLogging.Shutdown();
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            singleInstance?.Dispose();
            AppLogging.Shutdown();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
