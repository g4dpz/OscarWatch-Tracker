using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using OscarWatch.Localization;
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
                Title = LocalizationService.Instance.Get("Settings.SaveFailed.Title"),
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

    private async void OnBrowseRecordingFolderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            await vm.BrowseRecordingOutputFolderAsync(this).ConfigureAwait(true);
    }

    private async void OnBrowseTleFileClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            await vm.BrowseTleLocalFileAsync(this).ConfigureAwait(true);
    }

    private async void OnTestHamsAtClick(object? sender, RoutedEventArgs e)
    {
        var testButton = sender as Button;
        if (testButton is not null)
            testButton.IsEnabled = false;

        try
        {
            TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();

            if (DataContext is SettingsViewModel vm)
                await vm.TestHamsAtAsync().ConfigureAwait(true);
        }
        finally
        {
            if (testButton is not null)
                testButton.IsEnabled = true;
        }
    }

    private async void OnTestCloudlogClick(object? sender, RoutedEventArgs e)
    {
        var testButton = sender as Button;
        if (testButton is not null)
            testButton.IsEnabled = false;

        try
        {
            TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();

            if (DataContext is SettingsViewModel vm)
                await vm.TestCloudlogAsync().ConfigureAwait(true);
        }
        finally
        {
            if (testButton is not null)
                testButton.IsEnabled = true;
        }
    }
}
