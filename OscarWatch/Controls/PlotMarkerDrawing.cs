using Avalonia;
using Avalonia.Media;
using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;

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

    public static void DrawRemoteStationMarker(DrawingContext context, double x, double y)
    {
        const double radius = 7;
        var fill = Color.Parse("#E69F00");
        var outline = Color.Parse("#1a2028");

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(x, y - radius), true);
            ctx.LineTo(new Point(x + radius, y));
            ctx.LineTo(new Point(x, y + radius));
            ctx.LineTo(new Point(x - radius, y));
            ctx.EndFigure(true);
        }

        context.DrawGeometry(new SolidColorBrush(fill), new Pen(new SolidColorBrush(outline), 2.5), geometry);
        context.DrawGeometry(null, new Pen(Brushes.White, 1.5), geometry);
    }

    public static void DrawFootprintMotionArrow(
        DrawingContext context,
        GeoCoordinate subpoint,
        double headingDeg,
        double footprintRadiusDeg,
        double centerX,
        double centerY,
        double mapWidth,
        double mapHeight,
        Color color,
        bool isFocused)
    {
        var (endLat, endLon) = SphericalGeo.DestinationPoint(
            subpoint.LatitudeDeg,
            subpoint.LongitudeDeg,
            2.0,
            headingDeg);
        endLon = EquirectangularProjection.NormalizeLongitudeNear(endLon, subpoint.LongitudeDeg);

        var (rawX, rawY) = EquirectangularProjection.GeoToPixel(
            subpoint.LatitudeDeg,
            subpoint.LongitudeDeg,
            mapWidth,
            mapHeight);
        var (endX, endY) = EquirectangularProjection.GeoToPixel(
            endLat,
            endLon,
            mapWidth,
            mapHeight);
        endX += centerX - rawX;

        var dx = endX - centerX;
        var dy = endY - centerY;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 2)
            return;

        dx /= len;
        dy /= len;

        var radiusPx = footprintRadiusDeg / 180.0 * mapHeight;
        var halfLen = Math.Clamp(radiusPx * 0.38, 7, isFocused ? 24 : 20);
        var wing = halfLen * 0.55;

        var tipX = centerX + dx * halfLen;
        var tipY = centerY + dy * halfLen;
        var baseX = centerX - dx * halfLen * 0.45;
        var baseY = centerY - dy * halfLen * 0.45;
        var leftX = tipX - dx * wing + dy * wing * 0.6;
        var leftY = tipY - dy * wing - dx * wing * 0.6;
        var rightX = tipX - dx * wing - dy * wing * 0.6;
        var rightY = tipY - dy * wing + dx * wing * 0.6;

        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(new Point(tipX, tipY), true);
            g.LineTo(new Point(leftX, leftY));
            g.LineTo(new Point(baseX, baseY));
            g.LineTo(new Point(rightX, rightY));
            g.EndFigure(true);
        }

        var fill = Color.FromArgb((byte)(isFocused ? 220 : 190), color.R, color.G, color.B);
        context.DrawGeometry(
            new SolidColorBrush(fill),
            new Pen(new SolidColorBrush(Color.FromArgb(240, 26, 32, 40)), isFocused ? 2 : 1.5),
            geometry);
    }
}
