using Avalonia;
using Avalonia.Media;
using OscarWatch.Core.Display;
using OscarWatch.Core.Models;

namespace OscarWatch.Controls;

/// <summary>Draws the obstructed band under a horizon mask on polar sky plots.</summary>
internal static class HorizonMaskPlotDrawing
{
    private static readonly Color ObstructionFill = Color.FromArgb(70, 80, 90, 100);
    private static readonly Color ObstructionStroke = Color.FromArgb(160, 90, 100, 110);

    public static void DrawObstruction(
        DrawingContext context,
        double cx,
        double cy,
        double plotRadius,
        HorizonMask? mask,
        RenderResourceCache renderCache)
    {
        if (mask is null || !mask.HasPoints || plotRadius <= 0)
            return;

        var skyline = HorizonMaskPolarGeometry.SampleSkyline(mask);
        if (skyline.Count < 3)
            return;

        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            var started = false;
            foreach (var (az, el) in skyline)
            {
                if (!SkyPlotControl.TryAzElToPoint(cx, cy, plotRadius, az, el, out var pt))
                    continue;
                var point = new Point(pt.X, pt.Y);
                if (!started)
                {
                    g.BeginFigure(point, isFilled: true);
                    started = true;
                }
                else
                    g.LineTo(point);
            }

            // Close along outer horizon (el = 0) in reverse azimuth order.
            for (var i = skyline.Count - 1; i >= 0; i--)
            {
                var az = skyline[i].AzimuthDeg;
                if (!SkyPlotControl.TryAzElToPoint(cx, cy, plotRadius, az, 0, out var outer))
                    continue;
                g.LineTo(new Point(outer.X, outer.Y));
            }

            if (started)
                g.EndFigure(isClosed: true);
        }

        context.DrawGeometry(
            renderCache.GetBrush(ObstructionFill),
            renderCache.GetPen(ObstructionStroke, 1.2),
            geometry);
    }
}
