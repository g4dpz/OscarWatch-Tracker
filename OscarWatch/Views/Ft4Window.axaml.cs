using Avalonia.Controls;
using Avalonia.Interactivity;
using OscarWatch.ViewModels;

namespace OscarWatch.Views;

public partial class Ft4Window : Window
{
    public Ft4Window()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is Ft4ViewModel vm)
            await vm.OnWindowOpenedAsync().ConfigureAwait(true);
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is Ft4ViewModel vm)
            await vm.OnWindowClosedAsync().ConfigureAwait(true);
    }

    private async void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not Ft4ViewModel vm)
            return;

        var dialog = new Ft4SettingsWindow { DataContext = vm };
        await dialog.ShowDialog(this).ConfigureAwait(true);
    }
}
