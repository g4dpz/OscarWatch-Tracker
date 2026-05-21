using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using OscarWatch.ViewModels;

namespace OscarWatch.Views;

public partial class SatelliteDatabaseWindow : Window
{
    public SatelliteDatabaseWindow()
    {
        InitializeComponent();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SatelliteDatabaseEditorViewModel vm)
            return;

        if (vm.TrySave(out var error))
        {
            Close(true);
            return;
        }

        await new Window
        {
            Title = "Could not save database",
            Width = 400,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new TextBlock
            {
                Text = error ?? "Unknown error",
                Margin = new Thickness(16),
                TextWrapping = TextWrapping.Wrap
            }
        }.ShowDialog(this);
    }
}
