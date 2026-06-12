// Task 2.3: Unit tests verifying SkyPlotControl cache invalidation on theme change

using Avalonia.Media;
using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Controls;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 2.4**
///
/// Unit and property-based tests verifying that calling <see cref="RenderResourceCache.Clear"/>
/// and <see cref="FormattedTextCache.Clear"/> (as triggered by a theme change) causes subsequent
/// requests to return new object references, proving the cache was invalidated and resources
/// will be rebuilt with the new palette colours.
/// </summary>
public class SkyPlotCacheInvalidationTests
{
    // --- Helper ---

    private static UiPalette CreatePalette(Color labelForeground, Color labelBackground) => new(
        SkyPlotBackground: Colors.Black,
        SkyPlotBorder: Colors.Gray,
        SkyPlotRing30: Colors.Gray,
        SkyPlotRing60: Colors.Gray,
        SkyPlotMinElRing: Colors.Blue,
        SkyPlotSpoke: Colors.Gray,
        SkyPlotLabel: Colors.White,
        SkyPlotMessage: Colors.White,
        MapFallbackBackground: Colors.DarkBlue,
        MapLabelBackground: labelBackground,
        MapLabelForeground: labelForeground,
        GroundStationFill: Colors.Blue,
        GroundStationOutlineDark: Colors.Black,
        SunlightTimeline: Colors.Yellow,
        EclipseTimeline: Colors.Gray,
        GreylineNightFill: Color.FromArgb(52, 48, 58, 88),
        GreylineTerminatorStroke: Color.FromArgb(90, 180, 190, 210));

    private static UiPalette CreateDarkPalette() =>
        CreatePalette(Colors.White, Color.FromArgb(220, 16, 20, 28));

    private static UiPalette CreateLightPalette() =>
        CreatePalette(Color.Parse("#1a2028"), Color.FromArgb(230, 248, 250, 252));

    // --- RenderResourceCache.Clear() invalidation tests ---

    /// <summary>
    /// After Clear(), GetBrush for the same colour returns a NEW brush instance.
    /// This proves theme change invalidation works for brushes.
    /// </summary>
    [Fact]
    public void RenderResourceCache_Clear_causes_GetBrush_to_return_new_reference()
    {
        var cache = new RenderResourceCache();
        var color = Colors.Red;

        var brushBefore = cache.GetBrush(color);
        cache.Clear();
        var brushAfter = cache.GetBrush(color);

        Assert.NotSame(brushBefore, brushAfter);
    }

    /// <summary>
    /// After Clear(), GetPen for the same colour/thickness returns a NEW pen instance.
    /// This proves theme change invalidation works for pens.
    /// </summary>
    [Fact]
    public void RenderResourceCache_Clear_causes_GetPen_to_return_new_reference()
    {
        var cache = new RenderResourceCache();
        var color = Color.Parse("#5a6a7a");
        const double thickness = 1.5;

        var penBefore = cache.GetPen(color, thickness);
        cache.Clear();
        var penAfter = cache.GetPen(color, thickness);

        Assert.NotSame(penBefore, penAfter);
    }

    /// <summary>
    /// After Clear(), GetDashedPen for the same colour/thickness returns a NEW pen instance.
    /// This proves theme change invalidation works for dashed pens.
    /// </summary>
    [Fact]
    public void RenderResourceCache_Clear_causes_GetDashedPen_to_return_new_reference()
    {
        var cache = new RenderResourceCache();
        var color = Colors.White;
        const double thickness = 1.5;

        var penBefore = cache.GetDashedPen(color, thickness);
        cache.Clear();
        var penAfter = cache.GetDashedPen(color, thickness);

        Assert.NotSame(penBefore, penAfter);
    }

    // --- FormattedTextCache.Clear() invalidation tests ---

    /// <summary>
    /// After Clear(), FormattedTextCache.Get for the same key returns a NEW instance.
    /// This proves theme change invalidation works for label text objects.
    /// </summary>
    [Fact]
    public void FormattedTextCache_Clear_causes_Get_to_return_new_reference()
    {
        var cache = new FormattedTextCache();
        var palette = CreateDarkPalette();

        var textBefore = cache.Get("N", 12.0, palette);
        cache.Clear();
        var textAfter = cache.Get("N", 12.0, palette);

        Assert.NotSame(textBefore, textAfter);
    }

    /// <summary>
    /// After Clear(), FormattedTextCache.GetLabelBrush returns a NEW brush instance.
    /// This proves theme change clears the foreground brush cache.
    /// </summary>
    [Fact]
    public void FormattedTextCache_Clear_causes_GetLabelBrush_to_return_new_reference()
    {
        var cache = new FormattedTextCache();
        var palette = CreateDarkPalette();

        var brushBefore = cache.GetLabelBrush(palette);
        cache.Clear();
        var brushAfter = cache.GetLabelBrush(palette);

        Assert.NotSame(brushBefore, brushAfter);
    }

    /// <summary>
    /// After Clear(), FormattedTextCache.GetBackgroundBrush returns a NEW brush instance.
    /// This proves theme change clears the background brush cache.
    /// </summary>
    [Fact]
    public void FormattedTextCache_Clear_causes_GetBackgroundBrush_to_return_new_reference()
    {
        var cache = new FormattedTextCache();
        var palette = CreateDarkPalette();

        var brushBefore = cache.GetBackgroundBrush(palette);
        cache.Clear();
        var brushAfter = cache.GetBackgroundBrush(palette);

        Assert.NotSame(brushBefore, brushAfter);
    }

    // --- Theme-switch simulation: Clear + re-get with different palette ---

    /// <summary>
    /// Simulates a full theme switch: cache populated with dark palette colours,
    /// Clear() called (theme change), then re-populated with light palette colours.
    /// The new brush must reflect the new theme's colour.
    /// </summary>
    [Fact]
    public void FormattedTextCache_theme_switch_produces_brush_with_new_colour()
    {
        var cache = new FormattedTextCache();
        var darkPalette = CreateDarkPalette();
        var lightPalette = CreateLightPalette();

        // Populate with dark theme
        var darkBrush = cache.GetLabelBrush(darkPalette);
        Assert.Equal(Colors.White, darkBrush.Color);

        // Simulate theme change
        cache.Clear();

        // Re-populate with light theme
        var lightBrush = cache.GetLabelBrush(lightPalette);
        Assert.Equal(Color.Parse("#1a2028"), lightBrush.Color);

        // Must be a different object
        Assert.NotSame(darkBrush, lightBrush);
    }

    // --- Property-based tests: for any random colour, Clear + re-get produces new reference ---

    /// <summary>
    /// Property: For any colour, after Clear(), GetBrush returns a new reference,
    /// proving cache invalidation works across the entire colour space.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool RenderResourceCache_Clear_always_invalidates_brush(byte a, byte r, byte g, byte b)
    {
        var color = Color.FromArgb(a, r, g, b);
        var cache = new RenderResourceCache();

        var before = cache.GetBrush(color);
        cache.Clear();
        var after = cache.GetBrush(color);

        return !ReferenceEquals(before, after);
    }

    /// <summary>
    /// Property: For any colour and thickness, after Clear(), GetPen returns a new reference.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool RenderResourceCache_Clear_always_invalidates_pen(byte a, byte r, byte g, byte b, PositiveInt thicknessRaw)
    {
        var color = Color.FromArgb(a, r, g, b);
        var thickness = (double)thicknessRaw.Get / 10.0;
        var cache = new RenderResourceCache();

        var before = cache.GetPen(color, thickness);
        cache.Clear();
        var after = cache.GetPen(color, thickness);

        return !ReferenceEquals(before, after);
    }

    /// <summary>
    /// Property: For any colour and thickness, after Clear(), GetDashedPen returns a new reference.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool RenderResourceCache_Clear_always_invalidates_dashed_pen(byte a, byte r, byte g, byte b, PositiveInt thicknessRaw)
    {
        var color = Color.FromArgb(a, r, g, b);
        var thickness = (double)thicknessRaw.Get / 10.0;
        var cache = new RenderResourceCache();

        var before = cache.GetDashedPen(color, thickness);
        cache.Clear();
        var after = cache.GetDashedPen(color, thickness);

        return !ReferenceEquals(before, after);
    }
}
