using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using OscarWatch.Core.Services;
using OscarWatch.Orbit;
using OscarWatch.Core.Models;
using OscarWatch.Cloudlog;
using OscarWatch.Recording;
using OscarWatch.Rig;
using OscarWatch.Rotator;
using OscarWatch.Speech;
using OscarWatch.Theme;
using OscarWatch.Diagnostics;
using OscarWatch.ViewModels;
using Serilog;

namespace OscarWatch;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ITleService, TleService>();
        services.AddSingleton<ISpeechService, PlatformSpeechService>();
        services.AddSingleton<IAudioRecordingService, PortAudioRecordingService>();
        services.AddSingleton<RisingPassAnnouncer>();
        services.AddSingleton<PassRecordingCoordinator>();
        services.AddSingleton<IRotatorController, RotatorController>();
        services.AddSingleton<IRigController, RigController>();
        services.AddSingleton<ICloudlogRadioSyncService, CloudlogRadioSyncService>();
        var bundledDb = Path.Combine(AppContext.BaseDirectory, "Assets", "satellite_database.json");
        services.AddSingleton<ISatelliteDatabaseService>(_ =>
            new SatelliteDatabaseService(bundledDb));
        services.AddSingleton<ISatelliteDatabaseEditor>(sp =>
            new SatelliteDatabaseEditor(
                sp.GetRequiredService<ISatelliteDatabaseService>(),
                bundledDb));
        services.AddSingleton<ISatelliteDatabaseSyncService, SatelliteDatabaseSyncService>();
        services.AddSingleton<FrequencyOverlayViewModel>();
        services.AddSingleton<DxStationOverlayViewModel>();
        services.AddSingleton<TrackingOrchestrator>();
        services.AddSingleton<LiveTrackingService>();
        services.AddSingleton<ILiveTrackingService>(sp => sp.GetRequiredService<LiveTrackingService>());
        services.AddOscarWatchOrbit();
        services.AddTransient<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SatellitePickerViewModel>();
        services.AddTransient<PassPlanningViewModel>();
        services.AddTransient<MutualPassViewModel>();
        services.AddTransient<SunlightPredictionViewModel>();
        services.AddTransient<SatelliteDatabaseEditorViewModel>();
        services.AddTransient<RotatorManualViewModel>();

        Services = services.BuildServiceProvider();

        var settings = Services.GetRequiredService<ISettingsService>();
        settings.Load();
        AppThemeManager.Apply(settings.Current.Theme);
        AccessibilityThemeResources.Install();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "dev";
            Log.Information("OscarWatch {Version} starting", version);

            var mainVm = Services.GetRequiredService<MainViewModel>();
            MainWindow = new MainWindow { DataContext = mainVm };
            desktop.MainWindow = MainWindow;

            AppSingleInstance.StartActivationListener(ActivateMainWindow);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ActivateMainWindow()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (MainWindow is null)
                return;

            if (MainWindow.WindowState == WindowState.Minimized)
                MainWindow.WindowState = WindowState.Normal;

            MainWindow.Show();
            MainWindow.Activate();
        });
    }
}
