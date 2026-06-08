// Feature: performance-optimisations, Property 2: Rendering resource cache identity
// Feature: performance-optimisations, Property 3: Cached rendering resource visual equivalence

using Avalonia.Media;
using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Controls;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 2.1, 2.2, 2.4, 4.1, 4.3**
///
/// Property-based tests verifying that <see cref="RenderResourceCache"/> returns the same
/// object reference for same-key lookups (Property 2), and that cached resources have
/// visually equivalent colour/thickness to freshly constructed equivalents (Property 3).
/// </summary>
public class RenderResourceCachePropertyTests
{
    /// <summary>
    /// Property 2: Rendering resource cache identity.
    ///
    /// For any colour value, requesting the same brush from RenderResourceCache twice
    /// without an intervening Clear() SHALL return the same object reference.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Same_color_brush_returns_same_reference(byte a, byte r, byte g, byte b)
    {
        var color = Color.FromArgb(a, r, g, b);
        var cache = new RenderResourceCache();

        var brush1 = cache.GetBrush(color);
        var brush2 = cache.GetBrush(color);

        return ReferenceEquals(brush1, brush2);
    }

    /// <summary>
    /// Property 2: Rendering resource cache identity.
    ///
    /// For any colour value and thickness, requesting the same pen from RenderResourceCache
    /// twice without an intervening Clear() SHALL return the same object reference.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Same_color_thickness_pen_returns_same_reference(byte a, byte r, byte g, byte b, PositiveInt thicknessRaw)
    {
        var color = Color.FromArgb(a, r, g, b);
        // Map to a positive thickness (PositiveInt guarantees > 0)
        var thickness = (double)thicknessRaw.Get / 10.0;
        var cache = new RenderResourceCache();

        var pen1 = cache.GetPen(color, thickness);
        var pen2 = cache.GetPen(color, thickness);

        return ReferenceEquals(pen1, pen2);
    }

    /// <summary>
    /// Property 3: Cached rendering resource visual equivalence.
    ///
    /// For any Color, the cached SolidColorBrush.Color SHALL equal the input colour,
    /// matching a freshly constructed new SolidColorBrush(color).
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Cached_brush_color_equals_input(byte a, byte r, byte g, byte b)
    {
        var color = Color.FromArgb(a, r, g, b);
        var cache = new RenderResourceCache();

        var cachedBrush = cache.GetBrush(color);
        var freshBrush = new SolidColorBrush(color);

        return cachedBrush.Color == freshBrush.Color;
    }

    /// <summary>
    /// Property 3: Cached rendering resource visual equivalence.
    ///
    /// For any Color and stroke thickness, the cached Pen's thickness and brush colour
    /// SHALL equal those of a freshly constructed Pen with the same parameters.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Cached_pen_thickness_and_color_equal_fresh(byte a, byte r, byte g, byte b, PositiveInt thicknessRaw)
    {
        var color = Color.FromArgb(a, r, g, b);
        var thickness = (double)thicknessRaw.Get / 10.0;
        var cache = new RenderResourceCache();

        var cachedPen = cache.GetPen(color, thickness);
        var freshPen = new Pen(new SolidColorBrush(color), thickness);

        // Verify thickness
        if (cachedPen.Thickness != freshPen.Thickness)
            return false;

        // Verify brush colour
        var cachedBrush = cachedPen.Brush as SolidColorBrush;
        var freshBrush = freshPen.Brush as SolidColorBrush;

        if (cachedBrush is null || freshBrush is null)
            return false;

        return cachedBrush.Color == freshBrush.Color;
    }
}
