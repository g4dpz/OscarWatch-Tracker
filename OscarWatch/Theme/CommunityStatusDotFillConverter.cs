using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using OscarWatch.Core.Services;

namespace OscarWatch.Theme;

public sealed class CommunityStatusDotFillConverter : IValueConverter
{
    public static CommunityStatusDotFillConverter Instance { get; } = new();

    private static readonly IBrush On = new SolidColorBrush(Color.Parse("#FF3DDC84"));
    private static readonly IBrush Off = new SolidColorBrush(Color.Parse("#FFE07070"));
    private static readonly IBrush Telem = new SolidColorBrush(Color.Parse("#FFE0A040"));
    private static readonly IBrush Unknown = new SolidColorBrush(Color.Parse("#FF909090"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is SatelliteCommunityStatusKind kind
            ? kind switch
            {
                SatelliteCommunityStatusKind.On => On,
                SatelliteCommunityStatusKind.Off => Off,
                SatelliteCommunityStatusKind.TelemetryOnly => Telem,
                _ => Unknown
            }
            : Unknown;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
