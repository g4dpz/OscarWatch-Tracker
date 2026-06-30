// Feature: render-path-allocation-reduction, Property 1: Cache lookup idempotency

using Avalonia.Media;
using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Controls;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 1.1, 1.2, 1.3**
///
/// Property-based tests verifying that <see cref="RenderResourceCache.GetPen"/> and
/// <see cref="RenderResourceCache.GetBrush"/> return the same object reference on
/// repeated calls with identical arguments (cache idempotency), and that
/// <see cref="RenderResourceCache.Clear"/> invalidates all cached entries so that
/// subsequent calls produce new instances.
/// </summary>
public class PassPolarPlotCacheIdempotencyPropertyTests
{
    /// <summary>
    /// Property 1: Cache lookup idempotency — GetPen returns same reference.
    ///
    /// For any random Color and positive thickness, calling GetPen twice with
    /// identical arguments SHALL return the same object reference.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool GetPen_same_args_returns_same_reference(byte a, byte r, byte g, byte b, PositiveInt thicknessRaw)
    {
        var color = Color.FromArgb(a, r, g, b);
        var thickness = (double)thicknessRaw.Get / 10.0;
        var cache = new RenderResourceCache();

        var pen1 = cache.GetPen(color, thickness);
        var pen2 = cache.GetPen(color, thickness);

        return ReferenceEquals(pen1, pen2);
    }

    /// <summary>
    /// Property 1: Cache lookup idempotency — GetBrush returns same reference.
    ///
    /// For any random Color, calling GetBrush twice with identical arguments
    /// SHALL return the same object reference.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool GetBrush_same_color_returns_same_reference(byte a, byte r, byte g, byte b)
    {
        var color = Color.FromArgb(a, r, g, b);
        var cache = new RenderResourceCache();

        var brush1 = cache.GetBrush(color);
        var brush2 = cache.GetBrush(color);

        return ReferenceEquals(brush1, brush2);
    }

    /// <summary>
    /// Property 1: Cache lookup idempotency — Clear invalidates cached pens.
    ///
    /// After calling Clear(), GetPen with the same arguments SHALL return a
    /// NEW instance that is NOT reference-equal to the previously cached pen.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool GetPen_after_Clear_returns_new_instance(byte a, byte r, byte g, byte b, PositiveInt thicknessRaw)
    {
        var color = Color.FromArgb(a, r, g, b);
        var thickness = (double)thicknessRaw.Get / 10.0;
        var cache = new RenderResourceCache();

        var penBefore = cache.GetPen(color, thickness);
        cache.Clear();
        var penAfter = cache.GetPen(color, thickness);

        return !ReferenceEquals(penBefore, penAfter);
    }
}
