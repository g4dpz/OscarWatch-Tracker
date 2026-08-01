using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Core.Services;
using OscarWatch.ViewModels;

namespace OscarWatch.Views;

public partial class PassElevationTimelineWindow : Window
{
    private bool _dockingFromClose;

    public PassElevationTimelineWindow()
    {
        InitializeComponent();
        var settings = App.Services.GetRequiredService<ISettingsService>();
        TimelineDetachedWindowBounds.Apply(this, settings.Current);
        Opened += OnOpened;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        var settings = App.Services.GetRequiredService<ISettingsService>();
        if (TimelineDetachedWindowDefaults.TryGetSavedPosition(settings.Current, out _, out _))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = TimelineDetachedWindowBounds.ClampToVisibleArea(this, Position);
        }

        TimelineControl.SatelliteFocusRequested += OnTimelineSatelliteFocusRequested;
        if (DataContext is MainViewModel vm)
        {
            TimelineControl.SetPropagator(
                App.Services.GetRequiredService<IOrbitPropagator>(),
                vm.GroundStation);
        }
    }

    private void OnTimelineSatelliteFocusRequested(object? sender, string noradId)
    {
        if (DataContext is MainViewModel vm)
            vm.FocusedNoradId = noradId;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        PersistBounds();
        if (_dockingFromClose || DataContext is not MainViewModel vm)
            return;

        // Closing the floating window docks the timeline back into the main window.
        _dockingFromClose = true;
        vm.DockPassElevationTimelineFromWindowClose();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        TimelineControl.SatelliteFocusRequested -= OnTimelineSatelliteFocusRequested;
        PersistBounds();
    }

    private void PersistBounds()
    {
        var settings = App.Services.GetRequiredService<ISettingsService>();
        TimelineDetachedWindowBounds.Capture(this, settings.Current);
        settings.RequestSave();
    }
}
