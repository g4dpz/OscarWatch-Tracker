using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using OscarWatch.ViewModels;

namespace OscarWatch.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button)
            button.IsEnabled = false;

        try
        {
            TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();

            if (DataContext is SettingsViewModel vm)
                await vm.SaveAsync();

            Close(true);
        }
        catch (Exception ex)
        {
            if (sender is Button btn)
                btn.IsEnabled = true;

            await new Window
            {
                Title = "Could not save settings",
                Width = 400,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new TextBlock
                {
                    Text = ex.Message,
                    Margin = new Thickness(16),
                    TextWrapping = TextWrapping.Wrap
                }
            }.ShowDialog(this);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
