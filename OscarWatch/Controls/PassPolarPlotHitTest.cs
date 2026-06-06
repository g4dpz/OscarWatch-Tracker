using Avalonia;
using OscarWatch.Core.Models;

namespace OscarWatch.Controls;

internal static class PassPolarPlotHitTest
{
    private const double HitRadiusPx = 14;

    internal readonly record struct HoverPoint(DateTime Utc, double AzimuthDeg, double ElevationDeg);

    public static HoverPoint? TryHit(
        PassPolarPlotData data,
        double cx,
        double cy,
        double plotRadius,
        Point pointer)
    {
        if (data.Samples.Count == 0)
            return null;

        HoverPoint? best = null;
        var bestDist = HitRadiusPx;

        for (var i = 0; i < data.Samples.Count; i++)
        {
            var sample = data.Samples[i];
            if (!SkyPlotControl.TryAzElToPoint(cx, cy, plotRadius, sample.AzimuthDeg, sample.ElevationDeg, out var point))
                continue;

            var dist = Distance(pointer, point);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = new HoverPoint(sample.Utc, sample.AzimuthDeg, sample.ElevationDeg);
            }
        }

        for (var i = 0; i < data.Samples.Count - 1; i++)
        {
            var a = data.Samples[i];
            var b = data.Samples[i + 1];
            if (!SkyPlotControl.TryAzElToPoint(cx, cy, plotRadius, a.AzimuthDeg, a.ElevationDeg, out var p1)
                || !SkyPlotControl.TryAzElToPoint(cx, cy, plotRadius, b.AzimuthDeg, b.ElevationDeg, out var p2))
                continue;

            var (dist, t) = DistanceToSegment(pointer, p1, p2);
            if (dist >= bestDist)
                continue;

            bestDist = dist;
            var utc = a.Utc + TimeSpan.FromTicks((long)((b.Utc - a.Utc).Ticks * t));
            var az = a.AzimuthDeg + (b.AzimuthDeg - a.AzimuthDeg) * t;
            var el = a.ElevationDeg + (b.ElevationDeg - a.ElevationDeg) * t;
            best = new HoverPoint(utc, az, el);
        }

        return best;
    }

    private static double Distance(Point a, (double X, double Y) b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    private static (double Distance, double T) DistanceToSegment(Point p, (double X, double Y) a, (double X, double Y) b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-6)
            return (Distance(p, a), 0);

        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0, 1);
        var projX = a.X + t * dx;
        var projY = a.Y + t * dy;
        return (Math.Sqrt(Math.Pow(p.X - projX, 2) + Math.Pow(p.Y - projY, 2)), t);
    }
}
