// Feature: performance-optimisations, Property 4: FormattedText cache correctness

using Avalonia.Media;
using FsCheck.Xunit;
using OscarWatch.Controls;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 3.1, 3.2, 3.3**
///
/// Property-based tests verifying that <see cref="FormattedTextCache"/> returns the same
/// instance for the same (name, fontSize) key, and a different instance for different keys.
/// </summary>
public class FormattedTextCachePropertyTests
{
    /// <summary>
    /// Creates a test palette with hardcoded colours for use in cache tests.
    /// </summary>
    private static UiPalette CreateTestPalette() => new(
        SkyPlotBackground: Colors.Black,
        SkyPlotBorder: Colors.Gray,
        SkyPlotRing30: Colors.Gray,
        SkyPlotRing60: Colors.Gray,
        SkyPlotMinElRing: Colors.Blue,
        SkyPlotSpoke: Colors.Gray,
        SkyPlotLabel: Colors.White,
        SkyPlotMessage: Colors.White,
        MapFallbackBackground: Colors.DarkBlue,
        MapLabelBackground: Color.FromArgb(230, 248, 250, 252),
        MapLabelForeground: Colors.White,
        GroundStationFill: Colors.Blue,
        GroundStationOutlineDark: Colors.Black,
        SunlightTimeline: Colors.Yellow,
        EclipseTimeline: Colors.Gray,
        GreylineNightFill: Color.FromArgb(52, 48, 58, 88),
        GreylineTerminatorStroke: Color.FromArgb(90, 180, 190, 210));

    /// <summary>
    /// Generates a satellite name (1–20 chars) from a seed byte array.
    /// </summary>
    private static string MakeName(byte[] seeds)
    {
        if (seeds is null || seeds.Length == 0)
            return "SAT";

        // Take 1–20 chars, mapped to printable ASCII range
        var length = Math.Max(1, Math.Min(seeds.Length, 20));
        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = (char)('A' + (seeds[i] % 26));

        return new string(chars);
    }

    /// <summary>
    /// Maps a byte to a font size in the range 8–16.
    /// </summary>
    private static double MakeFontSize(byte seed) => 8.0 + (seed % 9);

    /// <summary>
    /// Property 4: FormattedText cache correctness — same key returns same instance.
    ///
    /// For any (name, fontSize) pair, requesting a FormattedText from FormattedTextCache
    /// twice with the same key SHALL return the same instance (ReferenceEquals).
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Same_key_returns_same_instance(byte[] nameSeeds, byte fontSizeSeed)
    {
        var name = MakeName(nameSeeds);
        var fontSize = MakeFontSize(fontSizeSeed);
        var palette = CreateTestPalette();
        var cache = new FormattedTextCache();

        var text1 = cache.Get(name, fontSize, palette);
        var text2 = cache.Get(name, fontSize, palette);

        return ReferenceEquals(text1, text2);
    }

    /// <summary>
    /// Property 4: FormattedText cache correctness — different key returns different instance.
    ///
    /// For two different (name, fontSize) keys, requesting FormattedText from FormattedTextCache
    /// SHALL return different instances (not ReferenceEquals).
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Different_key_returns_different_instance(
        byte[] nameSeeds1, byte fontSizeSeed1,
        byte[] nameSeeds2, byte fontSizeSeed2)
    {
        var name1 = MakeName(nameSeeds1);
        var fontSize1 = MakeFontSize(fontSizeSeed1);
        var name2 = MakeName(nameSeeds2);
        var fontSize2 = MakeFontSize(fontSizeSeed2);

        // Only meaningful when keys are actually different
        if (name1 == name2 && fontSize1 == fontSize2)
            return true; // Trivially passes — keys are the same, not testable

        var palette = CreateTestPalette();
        var cache = new FormattedTextCache();

        var text1 = cache.Get(name1, fontSize1, palette);
        var text2 = cache.Get(name2, fontSize2, palette);

        return !ReferenceEquals(text1, text2);
    }
}
