using Avalonia;
using OscarWatch.Diagnostics;
using OscarWatch.Localization;
using OscarWatch.Services;
using Serilog;

namespace OscarWatch;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        LocalizationCulture.Apply(LocalizationCulture.ReadUiLanguageFromDisk());
        AppLogging.Configure();
        AppLogging.RegisterGlobalHandlers();
        Log.Information("OscarWatch process started; PID={ProcessId}", Environment.ProcessId);

        IDisposable? singleInstance = null;
        if (!AppSingleInstance.AllowsMultipleInstances(args)
            && !AppSingleInstance.TryBecomePrimaryInstance(out singleInstance))
        {
            if (AppSingleInstance.NotifyPrimaryInstance())
                Log.Information("Another OscarWatch instance is already running; activated existing window.");
            else
            {
                Log.Warning("Another OscarWatch instance appears to be running but could not be activated.");
                var localization = LocalizationService.Instance;
                SecondInstanceNotifier.Show(
                    localization.Get("App.AlreadyRunningTitle"),
                    localization.Get("App.AlreadyRunningMessage"));
            }

            AppLogging.Shutdown();
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            AppLogging.FlushFatal();
            throw;
        }
        finally
        {
            singleInstance?.Dispose();
            AppLogging.Shutdown();
            AppRestart.StartRequestedProcess();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var uiLanguage = LocalizationCulture.ReadUiLanguageFromDisk();
        return AppFontConfiguration.Configure(
                AppBuilder.Configure<App>()
                    .UsePlatformDetect()
                    .WithInterFont(),
                uiLanguage)
            .LogToTrace();
    }
}
