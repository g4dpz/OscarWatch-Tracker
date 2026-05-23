using Avalonia;
using OscarWatch.Diagnostics;

namespace OscarWatch;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppLogging.Configure();
        AppLogging.RegisterGlobalHandlers();
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
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
