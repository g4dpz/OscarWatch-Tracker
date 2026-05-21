using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using OscarWatch.Core.Services;
using OscarWatch.Orbit;
using OscarWatch.Core.Models;
using OscarWatch.Rig;
using OscarWatch.Rotator;
using OscarWatch.Speech;
using OscarWatch.Theme;
using OscarWatch.ViewModels;

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
        services.AddSingleton<RisingPassAnnouncer>();
        services.AddSingleton<IRotatorController, RotatorController>();
        services.AddSingleton<IRigController, RigController>();
        services.AddSingleton<ISatelliteDatabaseService>(_ =>
            new SatelliteDatabaseService(Path.Combine(AppContext.BaseDirectory, "Assets", "satellite_database.json")));
        services.AddSingleton<FrequencyOverlayViewModel>();
        services.AddSingleton<TrackingOrchestrator>();
        services.AddOscarWatchOrbit();
        services.AddTransient<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SatellitePickerViewModel>();
        services.AddTransient<PassPlanningViewModel>();
        services.AddTransient<MutualPassViewModel>();

        Services = services.BuildServiceProvider();

        var settings = Services.GetRequiredService<ISettingsService>();
        settings.Load();
        AppThemeManager.Apply(settings.Current.Theme);
        AccessibilityThemeResources.Install();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = Services.GetRequiredService<MainViewModel>();
            MainWindow = new MainWindow { DataContext = mainVm };
            desktop.MainWindow = MainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
