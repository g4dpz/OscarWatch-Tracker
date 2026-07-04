using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using OscarWatch.Localization;

namespace OscarWatch.Views;

public static class ExportAdifDialog
{
    /// <summary>Returns null when cancelled; otherwise whether satellite names should be remapped for LoTW.</summary>
    public static async Task<bool?> ShowAsync(Window owner)
    {
        var l = LocalizationService.Instance;
        bool? result = null;

        var exportButton = new Button
        {
            Content = l.Get("Logbook.ExportAdif.Dialog.Export"),
            Classes = { "dialog-footer", "accent" },
            MinWidth = 88,
            IsDefault = true
        };

        var cancelButton = new Button
        {
            Content = l.Get("Common.Cancel"),
            Classes = { "dialog-footer" },
            MinWidth = 88,
            IsCancel = true
        };

        var forLotwCheckBox = new CheckBox
        {
            Content = l.Get("Logbook.ExportAdif.Dialog.ForLotw"),
            IsChecked = false
        };

        var window = new Window
        {
            Title = l.Get("Logbook.ExportAdif.Dialog.Title"),
            Width = 460,
            MinHeight = 160,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = l.Get("Logbook.ExportAdif.Dialog.Intro"),
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brushes.Gray
                    },
                    forLotwCheckBox,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Margin = new Thickness(0, 8, 0, 0),
                        Children = { cancelButton, exportButton }
                    }
                }
            }
        };

        exportButton.Click += (_, _) =>
        {
            result = forLotwCheckBox.IsChecked == true;
            window.Close();
        };

        cancelButton.Click += (_, _) => window.Close();

        await window.ShowDialog(owner).ConfigureAwait(true);
        return result;
    }
}
