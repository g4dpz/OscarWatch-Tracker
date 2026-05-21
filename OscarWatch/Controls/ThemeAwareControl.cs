using Avalonia;
using Avalonia.Controls;

namespace OscarWatch.Controls;

public abstract class ThemeAwareControl : Control
{
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Application.Current is not null)
            Application.Current.ActualThemeVariantChanged += OnAppThemeChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (Application.Current is not null)
            Application.Current.ActualThemeVariantChanged -= OnAppThemeChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnAppThemeChanged(object? sender, EventArgs e) => InvalidateVisual();
}
