using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using OscarWatch.Core.Models;

namespace OscarWatch.Controls;

/// <summary>
/// Polar plot for a single satellite pass with sunlit/eclipse segments and mutual-window markers.
/// </summary>
public class PassPolarPlotControl : ThemeAwareControl
{
    private const double LabelMarginPx = 16;
    private static readonly Color EclipsePathColor = Color.Parse("#E05252");

    public static readonly StyledProperty<PassPolarPlotData?> PlotDataProperty =
        AvaloniaProperty.Register<PassPolarPlotControl, PassPolarPlotData?>(nameof(PlotData));

    public static readonly StyledProperty<double> MinimumElevationDegProperty =
        AvaloniaProperty.Register<PassPolarPlotControl, double>(nameof(MinimumElevationDeg), 0.0);

    static PassPolarPlotControl()
    {
        AffectsRender<PassPolarPlotControl>(PlotDataProperty, MinimumElevationDegProperty);
    }

    public PassPolarPlotControl()
    {
        ClipToBounds = true;
        MinHeight = 220;
    }

    public PassPolarPlotData? PlotData
    {
        get => GetValue(PlotDataProperty);
        set => SetValue(PlotDataProperty, value);
    }

    public double MinimumElevationDeg
    {
        get => GetValue(MinimumElevationDegProperty);
        set => SetValue(MinimumElevationDegProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0)
            return;

        var palette = UiPaletteResolver.Current;
        var local = new Rect(0, 0, w, h);
        var (cx, cy, plotRadius) = GetPlotGeometry(w, h);

        context.FillRectangle(new SolidColorBrush(palette.SkyPlotBackground), local);
        DrawHorizonDisk(context, cx, cy, plotRadius, palette);
        DrawElevationRing(context, cx, cy, plotRadius, 30, palette.SkyPlotRing30, 1);
        DrawElevationRing(context, cx, cy, plotRadius, 60, palette.SkyPlotRing60, 1);

        if (MinimumElevationDeg > 0 && MinimumElevationDeg < 90)
            DrawElevationRing(context, cx, cy, plotRadius, MinimumElevationDeg, palette.SkyPlotMinElRing, 1, dashed: true);

        DrawAzimuthSpokes(context, cx, cy, plotRadius, palette);
        DrawCardinalLabels(context, cx, cy, plotRadius, palette);

        var data = PlotData;
        if (data is null)
            return;

        foreach (var segment in data.Segments)
        {
            if (segment.Points.Count < 2)
                continue;

            var color = segment.IsSunlit ? palette.SunlightTimeline : EclipsePathColor;
            var pen = new Pen(new SolidColorBrush(color), 2.5)
            {
                LineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };

            var first = segment.Points[0];
            if (!SkyPlotControl.TryAzElToPoint(cx, cy, plotRadius, first.AzimuthDeg, first.ElevationDeg, out var prev))
                continue;

            for (var i = 1; i < segment.Points.Count; i++)
            {
                var point = segment.Points[i];
                if (!SkyPlotControl.TryAzElToPoint(cx, cy, plotRadius, point.AzimuthDeg, point.ElevationDeg, out var next))
                    continue;

                context.DrawLine(pen, new Point(prev.X, prev.Y), new Point(next.X, next.Y));
                prev = next;
            }
        }

        DrawMarker(context, cx, cy, plotRadius, data.MutualStart);
        DrawMarker(context, cx, cy, plotRadius, data.MutualEnd);
    }

    private static void DrawMarker(
        DrawingContext context,
        double cx,
        double cy,
        double plotRadius,
        PassPolarPlotMarker? marker)
    {
        if (marker is null)
            return;

        if (!SkyPlotControl.TryAzElToPoint(cx, cy, plotRadius, marker.AzimuthDeg, marker.ElevationDeg, out var point))
            return;

        switch (marker.Kind)
        {
            case PassPolarPlotMarkerKind.MutualWindowStart:
                PlotMarkerDrawing.DrawMutualWindowStartMarker(context, point.X, point.Y);
                break;
            case PassPolarPlotMarkerKind.MutualWindowEnd:
                PlotMarkerDrawing.DrawMutualWindowEndMarker(context, point.X, point.Y);
                break;
        }
    }

    private static (double Cx, double Cy, double PlotRadius) GetPlotGeometry(double width, double height)
    {
        var side = Math.Min(width, height);
        var cx = width / 2;
        var cy = height / 2;
        var plotRadius = Math.Max(0, side / 2 - LabelMarginPx);
        return (cx, cy, plotRadius);
    }

    private static void DrawHorizonDisk(DrawingContext context, double cx, double cy, double plotRadius, UiPalette palette)
    {
        var disk = new Rect(cx - plotRadius, cy - plotRadius, plotRadius * 2, plotRadius * 2);
        context.DrawEllipse(
            new SolidColorBrush(palette.SkyPlotBackground),
            new Pen(new SolidColorBrush(palette.SkyPlotBorder), 1.5),
            disk);
    }

    private static void DrawElevationRing(
        DrawingContext context,
        double cx,
        double cy,
        double plotRadius,
        double elevationDeg,
        Color color,
        double thickness,
        bool dashed = false)
    {
        var r = (90.0 - Math.Clamp(elevationDeg, 0, 90)) / 90.0 * plotRadius;
        var pen = new Pen(new SolidColorBrush(color), thickness);
        if (dashed)
            pen.DashStyle = DashStyle.Dash;

        context.DrawEllipse(null, pen, new Rect(cx - r, cy - r, r * 2, r * 2));
    }

    private static void DrawAzimuthSpokes(DrawingContext context, double cx, double cy, double plotRadius, UiPalette palette)
    {
        var pen = new Pen(new SolidColorBrush(palette.SkyPlotSpoke), 1);
        for (var az = 0; az < 360; az += 45)
        {
            if (!SkyPlotControl.TryAzElToPoint(cx, cy, plotRadius, az, 0, out var spokeEnd))
                continue;

            context.DrawLine(pen, new Point(cx, cy), new Point(spokeEnd.X, spokeEnd.Y));
        }
    }

    private static void DrawCardinalLabels(DrawingContext context, double cx, double cy, double plotRadius, UiPalette palette)
    {
        DrawLabel(context, "N", cx, cy - plotRadius - 14, palette);
        DrawLabel(context, "S", cx, cy + plotRadius + 4, palette);
        DrawLabel(context, "E", cx + plotRadius + 6, cy - 5, palette);
        DrawLabel(context, "W", cx - plotRadius - 18, cy - 5, palette);
    }

    private static void DrawLabel(DrawingContext context, string text, double x, double y, UiPalette palette)
    {
        var ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold),
            12,
            new SolidColorBrush(palette.SkyPlotLabel));
        context.DrawText(ft, new Point(x, y));
    }
}
