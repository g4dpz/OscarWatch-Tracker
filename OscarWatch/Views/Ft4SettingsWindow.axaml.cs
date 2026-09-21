using Avalonia.Controls;
using Avalonia.Interactivity;
using OscarWatch.ViewModels;

namespace OscarWatch.Views;

public partial class Ft4SettingsWindow : Window
{
    public Ft4SettingsWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        if (DataContext is Ft4ViewModel vm)
            vm.RefreshDevicesCommand.Execute(null);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
