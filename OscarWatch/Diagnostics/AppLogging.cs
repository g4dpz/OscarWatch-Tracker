using System.Diagnostics;
using Avalonia.Threading;
using OscarWatch.Core.Services;
using Serilog;
using Serilog.Events;

namespace OscarWatch.Diagnostics;

/// <summary>Serilog file logging under %AppData%/OscarWatch/logs.</summary>
public static class AppLogging
{
    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OscarWatch",
        "logs");

    public static void Configure()
    {
        Directory.CreateDirectory(LogDirectory);

        var logPath = Path.Combine(LogDirectory, "oscarwatch-.log");
#if DEBUG
        var minimumLevel = LogEventLevel.Debug;
#else
        var minimumLevel = LogEventLevel.Information;
#endif

        var config = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.WithProperty("Application", "OscarWatch")
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}");

#if DEBUG
        config = config.WriteTo.Trace(LogEventLevel.Debug);
#endif

        Log.Logger = config.CreateLogger();

        Log.Information("OscarWatch logging started. Log directory: {LogDirectory}", LogDirectory);
    }

    public static void RegisterGlobalHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Log.Fatal(ex, "Unhandled AppDomain exception (terminating={IsTerminating})", e.IsTerminating);
            else
                Log.Fatal("Unhandled AppDomain exception: {ExceptionObject}", e.ExceptionObject);
            FlushFatal();
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Unobserved task exception");
            e.SetObserved();
            FlushFatal();
        };

        SettingsService.SaveFailed += ex =>
            Log.Warning(ex, "Settings save failed");
    }

    public static void RegisterAvaloniaHandlers()
    {
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Log.Fatal(e.Exception, "Unhandled UI thread exception");
            FlushFatal();
        };
    }

    public static void FlushFatal() => Log.CloseAndFlush();

    public static void Shutdown()
    {
        Log.Information("OscarWatch shutting down");
        Log.CloseAndFlush();
    }

    public static void OpenLogDirectory()
    {
        Directory.CreateDirectory(LogDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = LogDirectory,
            UseShellExecute = true
        });
    }
}
