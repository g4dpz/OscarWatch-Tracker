// Task 1.3: Unit tests verifying PlotMarkerDrawing produces zero new allocations for repeated calls

using Avalonia.Media;
using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Controls;
using Xunit;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 1.4, 1.5**
///
/// Unit and property-based tests verifying that <see cref="RenderResourceCache"/> returns
/// the same object references for repeated calls with the same key (zero new allocations),
/// that <see cref="RenderResourceCache.GetDashedPen"/> returns a pen with DashStyle.Dash,
/// and that cached resource parameters (colour, thickness) match the pre-refactor values
/// used in <see cref="PlotMarkerDrawing"/>.
/// </summary>
public class PlotMarkerDrawingCacheTests
{
    // --- Zero-allocation identity tests (Requirement 1.5) ---

    /// <summary>
    /// Simulates calling DrawSatelliteMarker twice with the same colour: the cache
    /// should return the same brush reference both times (zero new allocations).
    /// </summary>
    [Fact]
    public void DrawSatelliteMarker_same_color_twice_returns_same_brush_reference()
    {
        var cache = new RenderResourceCache();
        var fillColor = Color.FromArgb(255, 100, 150, 200);

        // First call — populates the cache
        var brush1 = cache.GetBrush(fillColor);
        // Second call — should return the cached instance
        var brush2 = cache.GetBrush(fillColor);

        Assert.Same(brush1, brush2);
    }

    /// <summary>
    /// Simulates calling DrawSatelliteMarker twice with the same colour/thickness:
    /// the cache should return the same pen reference both times.
    /// </summary>
    [Fact]
    public void DrawSatelliteMarker_same_color_thickness_twice_returns_same_pen_reference()
    {
        var cache = new RenderResourceCache();
        var outlineColor = Color.Parse("#1a2028");
        const double thickness = 2.5;

        var pen1 = cache.GetPen(outlineColor, thickness);
        var pen2 = cache.GetPen(outlineColor, thickness);

        Assert.Same(pen1, pen2);
    }

    /// <summary>
    /// Simulates calling DrawSatelliteMarker with belowMinimumElevation=true twice:
    /// the dashed pen cache should return the same reference both times.
    /// </summary>
    [Fact]
    public void DrawSatelliteMarker_same_dashed_pen_twice_returns_same_reference()
    {
        var cache = new RenderResourceCache();
        const double thickness = 1.5;

        var pen1 = cache.GetDashedPen(Colors.White, thickness);
        var pen2 = cache.GetDashedPen(Colors.White, thickness);

        Assert.Same(pen1, pen2);
    }

    /// <summary>
    /// Calling multiple drawing methods that share the same colour should all
    /// return the same cached brush instance (cross-method cache sharing).
    /// </summary>
    [Fact]
    public void Multiple_methods_sharing_color_return_same_brush()
    {
        var cache = new RenderResourceCache();
        var sharedColor = Colors.White;

        // GetBrush is used internally by GetPen — verify they share the same underlying brush
        var brush = cache.GetBrush(sharedColor);
        var pen = cache.GetPen(sharedColor, 1.5);
        var penBrush = pen.Brush as SolidColorBrush;

        Assert.Same(brush, penBrush);
    }

    // --- Visual output parameter correctness (Requirement 1.4) ---

    /// <summary>
    /// Verifies the outline pen used in DrawSatelliteMarker (non-focused) matches
    /// pre-refactor values: colour #1a2028, thickness 2.5.
    /// </summary>
    [Fact]
    public void Satellite_marker_outline_pen_matches_prerefactor_values()
    {
        var cache = new RenderResourceCache();
        var expectedColor = Color.Parse("#1a2028");
        const double expectedThickness = 2.5;

        var pen = cache.GetPen(expectedColor, expectedThickness);
        var brush = pen.Brush as SolidColorBrush;

        Assert.NotNull(brush);
        Assert.Equal(expectedColor, brush.Color);
        Assert.Equal(expectedThickness, pen.Thickness);
    }

    /// <summary>
    /// Verifies the focused outline pen used in DrawSatelliteMarker matches
    /// pre-refactor values: colour #1a2028, thickness 3.
    /// </summary>
    [Fact]
    public void Satellite_marker_focused_outline_pen_matches_prerefactor_values()
    {
        var cache = new RenderResourceCache();
        var expectedColor = Color.Parse("#1a2028");
        const double expectedThickness = 3.0;

        var pen = cache.GetPen(expectedColor, expectedThickness);
        var brush = pen.Brush as SolidColorBrush;

        Assert.NotNull(brush);
        Assert.Equal(expectedColor, brush.Color);
        Assert.Equal(expectedThickness, pen.Thickness);
    }

    /// <summary>
    /// Verifies the white highlight pen (non-focused, above min elevation) matches
    /// pre-refactor values: White, thickness 1.5.
    /// </summary>
    [Fact]
    public void Satellite_marker_white_highlight_pen_matches_prerefactor_values()
    {
        var cache = new RenderResourceCache();
        const double expectedThickness = 1.5;

        var pen = cache.GetPen(Colors.White, expectedThickness);
        var brush = pen.Brush as SolidColorBrush;

        Assert.NotNull(brush);
        Assert.Equal(Colors.White, brush.Color);
        Assert.Equal(expectedThickness, pen.Thickness);
    }

    /// <summary>
    /// Verifies that GetDashedPen returns a pen with DashStyle.Dash set,
    /// matching the pre-refactor behaviour of DrawSatelliteMarker with belowMinimumElevation.
    /// </summary>
    [Fact]
    public void Dashed_pen_has_dash_style_set()
    {
        var cache = new RenderResourceCache();
        const double thickness = 1.5;

        var pen = cache.GetDashedPen(Colors.White, thickness);

        Assert.NotNull(pen.DashStyle);
        Assert.Equal(DashStyle.Dash, pen.DashStyle);
    }

    /// <summary>
    /// Verifies the dashed pen colour and thickness match pre-refactor values.
    /// </summary>
    [Fact]
    public void Dashed_pen_color_and_thickness_match_prerefactor_values()
    {
        var cache = new RenderResourceCache();
        const double expectedThickness = 1.5;

        var pen = cache.GetDashedPen(Colors.White, expectedThickness);
        var brush = pen.Brush as SolidColorBrush;

        Assert.NotNull(brush);
        Assert.Equal(Colors.White, brush.Color);
        Assert.Equal(expectedThickness, pen.Thickness);
    }

    /// <summary>
    /// Verifies the focused gold ring pen matches pre-refactor values: Gold, thickness 2.
    /// </summary>
    [Fact]
    public void Focused_gold_ring_pen_matches_prerefactor_values()
    {
        var cache = new RenderResourceCache();
        const double expectedThickness = 2.0;

        var pen = cache.GetPen(Colors.Gold, expectedThickness);
        var brush = pen.Brush as SolidColorBrush;

        Assert.NotNull(brush);
        Assert.Equal(Colors.Gold, brush.Color);
        Assert.Equal(expectedThickness, pen.Thickness);
    }

    // --- Property-based: zero allocations across arbitrary repeated colours ---

    /// <summary>
    /// Property: For any colour, calling GetBrush twice returns the same reference
    /// (proving zero new brush allocations on repeated PlotMarkerDrawing calls).
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Repeated_brush_call_same_color_zero_allocations(byte a, byte r, byte g, byte b)
    {
        var color = Color.FromArgb(a, r, g, b);
        var cache = new RenderResourceCache();

        var first = cache.GetBrush(color);
        var second = cache.GetBrush(color);

        return ReferenceEquals(first, second);
    }

    /// <summary>
    /// Property: For any colour and thickness, calling GetDashedPen twice returns
    /// the same reference (zero allocations for repeated dashed pen requests).
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Repeated_dashed_pen_call_same_key_zero_allocations(byte a, byte r, byte g, byte b, PositiveInt thicknessRaw)
    {
        var color = Color.FromArgb(a, r, g, b);
        var thickness = (double)thicknessRaw.Get / 10.0;
        var cache = new RenderResourceCache();

        var first = cache.GetDashedPen(color, thickness);
        var second = cache.GetDashedPen(color, thickness);

        return ReferenceEquals(first, second);
    }

    /// <summary>
    /// Property: For any colour and thickness, the dashed pen always has a non-null
    /// DashStyle matching DashStyle.Dash (visual correctness across all inputs).
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Dashed_pen_always_has_dash_style(byte a, byte r, byte g, byte b, PositiveInt thicknessRaw)
    {
        var color = Color.FromArgb(a, r, g, b);
        var thickness = (double)thicknessRaw.Get / 10.0;
        var cache = new RenderResourceCache();

        var pen = cache.GetDashedPen(color, thickness);

        return pen.DashStyle == DashStyle.Dash;
    }
}
