using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using OscarWatch.Localization;

namespace OscarWatch.Views;

public static class Ts2000SatlWarningDialog
{
    public static async Task ShowAsync(Window owner, ILocalizationService localization)
    {
        var okButton = new Button
        {
            Content = localization.Get("Common.Ok"),
            MinWidth = 88,
            IsDefault = true
        };

        var window = new Window
        {
            Title = localization.Get("Rig.Ts2000SatlWarningTitle"),
            Width = 480,
            MinHeight = 180,
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
                        Text = localization.Get("Rig.Ts2000SatlWarningMessage"),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children = { okButton }
                    }
                }
            }
        };

        okButton.Click += (_, _) => window.Close();

        await window.ShowDialog(owner).ConfigureAwait(true);
    }
}
