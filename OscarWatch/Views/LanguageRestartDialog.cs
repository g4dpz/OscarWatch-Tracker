using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using OscarWatch.Localization;

namespace OscarWatch.Views;

public static class LanguageRestartDialog
{
    public static async Task<bool> ShowAsync(Window owner)
    {
        var l = LocalizationService.Instance;
        var restart = false;

        var restartButton = new Button
        {
            Content = l.Get("Settings.Language.RestartPrompt.RestartNow"),
            MinWidth = 100,
            IsDefault = true,
            Classes = { "accent" }
        };

        var laterButton = new Button
        {
            Content = l.Get("Settings.Language.RestartPrompt.Later"),
            MinWidth = 88,
            IsCancel = true
        };

        var window = new Window
        {
            Title = l.Get("Settings.Language.RestartPrompt.Title"),
            Width = 440,
            MinHeight = 160,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = l.Get("Settings.Language.RestartPrompt.Message"),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { laterButton, restartButton }
                    }
                }
            }
        };

        restartButton.Click += (_, _) =>
        {
            restart = true;
            window.Close();
        };

        laterButton.Click += (_, _) => window.Close();

        await window.ShowDialog(owner).ConfigureAwait(true);
        return restart;
    }
}
