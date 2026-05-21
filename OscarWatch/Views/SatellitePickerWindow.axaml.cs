using Avalonia.Controls;
using Avalonia.Interactivity;
using OscarWatch.ViewModels;

namespace OscarWatch.Views;

public partial class SatellitePickerWindow : Window
{
    public SatellitePickerWindow()
    {
        InitializeComponent();
    }

    private async void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SatellitePickerViewModel vm)
            await vm.SaveCommand.ExecuteAsync(null);
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
