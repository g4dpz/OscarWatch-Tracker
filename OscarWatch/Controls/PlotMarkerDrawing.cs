using Avalonia;
using Avalonia.Media;

namespace OscarWatch.Controls;

internal static class PlotMarkerDrawing
{
    private static readonly Color OutlineDark = Color.Parse("#1a2028");

    public static void DrawSatelliteMarker(
        DrawingContext context,
        double x,
        double y,
        Color fill,
        bool isFocused,
        bool belowMinimumElevation = false)
    {
        var radius = isFocused ? 7.0 : 5.0;
        var rect = new Rect(x - radius, y - radius, radius * 2, radius * 2);
        var fillColor = belowMinimumElevation
            ? Color.FromArgb(200, fill.R, fill.G, fill.B)
            : fill;

        context.DrawEllipse(
            new SolidColorBrush(fillColor),
            new Pen(new SolidColorBrush(OutlineDark), isFocused ? 3 : 2.5),
            rect);
        var lightPen = new Pen(Brushes.White, isFocused ? 2 : 1.5);
        if (belowMinimumElevation)
            lightPen.DashStyle = DashStyle.Dash;

        context.DrawEllipse(null, lightPen, rect);

        if (isFocused)
        {
            context.DrawEllipse(
                null,
                new Pen(new SolidColorBrush(Colors.Gold), 2),
                new Rect(x - radius - 4, y - radius - 4, (radius + 4) * 2, (radius + 4) * 2));
        }
    }

    public static void DrawGroundStationMarker(DrawingContext context, double x, double y, UiPalette palette)
    {
        const double radius = 6;
        var rect = new Rect(x - radius, y - radius, radius * 2, radius * 2);
        context.DrawEllipse(
            new SolidColorBrush(palette.GroundStationFill),
            new Pen(new SolidColorBrush(palette.GroundStationOutlineDark), 2.5),
            rect);
        context.DrawEllipse(null, new Pen(Brushes.White, 1.5), rect);
    }
}
