using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using OscarWatch.Localization;

namespace OscarWatch.Views;

public partial class PassVisualizerWindow : Window
{
    public PassVisualizerWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private async void OnScreenshotClick(object? sender, RoutedEventArgs e)
    {
        var root = ScreenshotRoot;
        if (root.Bounds.Width <= 0 || root.Bounds.Height <= 0)
            return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = LocalizationService.Instance.Get("Mutual.Visualizer.Screenshot"),
            SuggestedFileName = "pass-plot.png",
            DefaultExtension = "png",
            FileTypeChoices =
            [
                new FilePickerFileType("PNG image") { Patterns = ["*.png"] }
            ]
        });

        if (file is null)
            return;

        var width = Math.Max(1, (int)Math.Ceiling(root.Bounds.Width));
        var height = Math.Max(1, (int)Math.Ceiling(root.Bounds.Height));
        var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        bitmap.Render(root);
        await using var stream = await file.OpenWriteAsync();
        bitmap.Save(stream);
    }
}
