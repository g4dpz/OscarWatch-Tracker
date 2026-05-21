using Avalonia.Controls;
using Avalonia.Interactivity;
using OscarWatch.ViewModels;

namespace OscarWatch.Views;

public partial class PassPlanningWindow : Window
{
    public PassPlanningWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        if (DataContext is PassPlanningViewModel vm)
            await vm.RefreshPassesCommand.ExecuteAsync(null);
    }

    private async void OnExportSatelliteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: PassPlanningPassRow row }
            || DataContext is not PassPlanningViewModel vm)
            return;

        await vm.ExportSatelliteIcsAsync(this, row);
    }

    private async void OnUseActiveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PassPlanningViewModel vm)
            return;

        await vm.ApplyAsActiveStationAsync();
        Close(true);
    }

    private async void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PassPlanningViewModel vm)
            await vm.SaveFiltersAndStationsAsync();

        Close(false);
    }
}
