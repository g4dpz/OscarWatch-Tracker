using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using OscarWatch.ViewModels;

namespace OscarWatch.Theme;

public sealed class PassRowBadgeBackgroundConverter : IValueConverter
{
    public static readonly PassRowBadgeBackgroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not PassRowHighlight.Imminent
            || Application.Current?.Resources.TryGetResource(
                AccessibilityThemeResources.PassImminentBadgeBackgroundKey,
                Application.Current.ActualThemeVariant,
                out var brush) != true)
            return Brushes.Transparent;

        return brush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
