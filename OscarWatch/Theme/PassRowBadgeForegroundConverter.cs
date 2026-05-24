using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using OscarWatch.ViewModels;

namespace OscarWatch.Theme;

public sealed class PassRowBadgeForegroundConverter : IValueConverter
{
    public static readonly PassRowBadgeForegroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            PassRowHighlight.Imminent => AccessibilityThemeResources.PassImminentBadgeForegroundKey,
            PassRowHighlight.InProgress => AccessibilityThemeResources.PassInProgressBadgeForegroundKey,
            _ => null
        };

        if (key is null
            || Application.Current?.Resources.TryGetResource(
                key,
                Application.Current.ActualThemeVariant,
                out var brush) != true)
            return Brushes.Transparent;

        return brush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
