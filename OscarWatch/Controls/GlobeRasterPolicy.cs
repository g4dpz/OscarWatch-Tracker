using OscarWatch.Core.Geo;

namespace OscarWatch.Controls;

/// <summary>Everything that decides what the globe raster looks like.</summary>
internal readonly record struct RasterKey(
    GlobeCamera Camera,
    int SizePx,
    double Zoom,
    long UtcMinute,
    bool DarkTheme,
    bool MapAvailable);

/// <summary>When the Earth View needs a new raster, and how big it should be. Pure, so it can be tested.</summary>
internal static class GlobeRasterPolicy
{
    public const int MinRasterPx = 64;

    /// <summary>
    /// Largest raster edge. Measured at 5.5 ms for 768 pixels on a fast machine, and the cached hit table
    /// grows with the square of the size (about 9 MB at 768), so larger is not worth it. The raster is
    /// stretched to the control, and the overlays are drawn at full resolution on top.
    /// </summary>
    public const int MaxRasterPx = 768;

    /// <summary>Sizes are rounded to this step, so dragging a window edge does not render for every pixel.</summary>
    public const int SizeStepPx = 32;

    /// <summary>
    /// The subpoint must move more than this many degrees of arc before the raster is redone. At typical
    /// window sizes this is under one pixel, so the globe never visibly jumps. A low orbit satellite moves
    /// 0.05 degrees about once a second, which is well within what the renderer can afford.
    /// </summary>
    public const double SubpointToleranceDeg = 0.05;

    /// <summary>The altitude must change by more than this many kilometres. Matches the rasteriser's table tolerance.</summary>
    public const double AltitudeToleranceKm = 1.0;

    /// <summary>
    /// Raster edge for a control whose shorter side is <paramref name="sidePx"/> logical pixels on a display
    /// with the given scaling. Rounded to <see cref="SizeStepPx"/> and kept between the minimum and maximum.
    /// </summary>
    public static int RasterSize(double sidePx, double renderScaling)
    {
        if (!double.IsFinite(sidePx) || sidePx <= 0)
            return MinRasterPx;

        var scale = double.IsFinite(renderScaling) && renderScaling > 0 ? renderScaling : 1.0;
        var stepped = (int)(Math.Round(sidePx * scale / SizeStepPx) * SizeStepPx);
        return Math.Clamp(stepped, MinRasterPx, MaxRasterPx);
    }

    /// <summary>Whole minutes since the epoch. The night shading is redone when this changes.</summary>
    public static long UtcMinute(DateTime utc) => utc.Ticks / TimeSpan.TicksPerMinute;

    /// <summary>True when a raster made for <paramref name="current"/> would not do for <paramref name="desired"/>.</summary>
    public static bool NeedsRender(RasterKey? current, RasterKey desired)
    {
        if (current is not { } c)
            return true;

        return c.SizePx != desired.SizePx
            || c.Zoom != desired.Zoom
            || c.DarkTheme != desired.DarkTheme
            || c.MapAvailable != desired.MapAvailable
            || c.UtcMinute != desired.UtcMinute
            || Math.Abs(c.Camera.AltitudeKm - desired.Camera.AltitudeKm) > AltitudeToleranceKm
            || GlobeViewMath.CentralAngleDeg(
                   c.Camera.SubpointLatitudeDeg, c.Camera.SubpointLongitudeDeg,
                   desired.Camera.SubpointLatitudeDeg, desired.Camera.SubpointLongitudeDeg) > SubpointToleranceDeg;
    }
}
