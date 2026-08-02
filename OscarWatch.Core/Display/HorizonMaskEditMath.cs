namespace OscarWatch.Core.Display;

/// <summary>Az/el ↔ polar pixel maths and edit helpers for the horizon mask editor.</summary>
public static class HorizonMaskEditMath
{
    public const double AzimuthSnapDeg = 1.0;
    public const double ElevationSnapDeg = 0.5;

    public static bool TryAzElToPoint(
        double cx,
        double cy,
        double plotRadius,
        double azimuthDeg,
        double elevationDeg,
        out (double X, double Y) point)
    {
        if (elevationDeg < 0 || plotRadius <= 0)
        {
            point = default;
            return false;
        }

        var el = Math.Clamp(elevationDeg, 0, 90);
        var r = (90.0 - el) / 90.0 * plotRadius;
        var azRad = azimuthDeg * Math.PI / 180.0;
        point = (cx + r * Math.Sin(azRad), cy - r * Math.Cos(azRad));
        return true;
    }

    public static bool TryPointToAzEl(
        double cx,
        double cy,
        double plotRadius,
        double x,
        double y,
        out double azimuthDeg,
        out double elevationDeg)
    {
        azimuthDeg = 0;
        elevationDeg = 0;
        if (plotRadius <= 0)
            return false;

        var dx = x - cx;
        var dy = cy - y;
        var r = Math.Sqrt(dx * dx + dy * dy);
        if (r > plotRadius * 1.05)
            return false;

        var el = 90.0 * (1.0 - Math.Min(r, plotRadius) / plotRadius);
        var az = Math.Atan2(dx, dy) * 180.0 / Math.PI;
        if (az < 0)
            az += 360.0;

        azimuthDeg = SnapAzimuth(az);
        elevationDeg = SnapElevation(el);
        return true;
    }

    public static double SnapAzimuth(double azimuthDeg)
    {
        var az = Math.Round(azimuthDeg / AzimuthSnapDeg) * AzimuthSnapDeg;
        az %= 360.0;
        if (az < 0)
            az += 360.0;
        if (az >= 360.0 - 1e-9)
            az = 0;
        return az;
    }

    public static double SnapElevation(double elevationDeg) =>
        Math.Clamp(Math.Round(elevationDeg / ElevationSnapDeg) * ElevationSnapDeg, 0, 90);

    public static int FindNearestPointIndex(
        IReadOnlyList<(double AzimuthDeg, double ElevationDeg)> points,
        double cx,
        double cy,
        double plotRadius,
        double x,
        double y,
        double hitRadiusPx)
    {
        var best = -1;
        var bestDist = hitRadiusPx;
        for (var i = 0; i < points.Count; i++)
        {
            if (!TryAzElToPoint(cx, cy, plotRadius, points[i].AzimuthDeg, points[i].ElevationDeg, out var pt))
                continue;
            var dist = Math.Sqrt((x - pt.X) * (x - pt.X) + (y - pt.Y) * (y - pt.Y));
            if (dist <= bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }

        return best;
    }
}
