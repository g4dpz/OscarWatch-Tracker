using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using OscarWatch.ViewModels;

namespace OscarWatch.Theme;

public sealed class PassRowBackgroundConverter : IValueConverter
{
    public static readonly PassRowBackgroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            PassRowHighlight.InProgress or PassRowHighlight.Recording =>
                AccessibilityThemeResources.PassInProgressBackgroundKey,
            PassRowHighlight.Imminent => AccessibilityThemeResources.PassImminentBackgroundKey,
            _ => null
        };

        if (key is not null
            && Application.Current?.Resources.TryGetResource(
                key, Application.Current.ActualThemeVariant, out var brush) == true)
            return brush;

        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
