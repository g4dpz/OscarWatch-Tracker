using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using OscarWatch.Localization;

namespace OscarWatch.Views;

public static class SimpleConfirmDialog
{
    public static async Task<bool> ShowAsync(Window owner, string title, string message)
    {
        var l = LocalizationService.Instance;
        var result = false;

        var okButton = new Button
        {
            Content = l.Get("Common.Ok"),
            MinWidth = 88,
            IsDefault = true
        };

        var cancelButton = new Button
        {
            Content = l.Get("Common.Cancel"),
            MinWidth = 88,
            IsCancel = true
        };

        var window = new Window
        {
            Title = title,
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
                        Text = message,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { cancelButton, okButton }
                    }
                }
            }
        };

        okButton.Click += (_, _) =>
        {
            result = true;
            window.Close();
        };

        cancelButton.Click += (_, _) => window.Close();

        await window.ShowDialog(owner).ConfigureAwait(true);
        return result;
    }
}
