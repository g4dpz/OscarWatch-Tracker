using System.Globalization;
using Avalonia.Data.Converters;
using OscarWatch.Rotator;

namespace OscarWatch.Theme;

public sealed class SerialPortDisplayConverter : IValueConverter
{
    public static readonly SerialPortDisplayConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            string path => SerialPortPathFormatter.FormatDisplay(path),
            _ => value
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
