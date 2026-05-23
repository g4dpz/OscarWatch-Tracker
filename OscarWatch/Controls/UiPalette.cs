using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace OscarWatch.Controls;

internal readonly record struct UiPalette(
    Color SkyPlotBackground,
    Color SkyPlotBorder,
    Color SkyPlotRing30,
    Color SkyPlotRing60,
    Color SkyPlotMinElRing,
    Color SkyPlotSpoke,
    Color SkyPlotLabel,
    Color SkyPlotMessage,
    Color MapFallbackBackground,
    Color MapLabelBackground,
    Color MapLabelForeground,
    Color GroundStationFill,
    Color GroundStationOutlineDark,
    Color SunlightTimeline,
    Color EclipseTimeline);

internal static class UiPaletteResolver
{
    private static readonly UiPalette Light = new(
        SkyPlotBackground: Color.Parse("#eef1f5"),
        SkyPlotBorder: Color.Parse("#6b7a8a"),
        SkyPlotRing30: Color.Parse("#c5ced8"),
        SkyPlotRing60: Color.Parse("#d5dde6"),
        SkyPlotMinElRing: Color.Parse("#0072B2"),
        SkyPlotSpoke: Color.Parse("#8a96a6"),
        SkyPlotLabel: Color.Parse("#2d3748"),
        SkyPlotMessage: Color.Parse("#4a5568"),
        MapFallbackBackground: Color.Parse("#c8d4e0"),
        MapLabelBackground: Color.FromArgb(230, 248, 250, 252),
        MapLabelForeground: Color.Parse("#1a2028"),
        GroundStationFill: Color.Parse("#0072B2"),
        GroundStationOutlineDark: Color.Parse("#1a2028"),
        SunlightTimeline: Color.Parse("#D4A017"),
        EclipseTimeline: Color.Parse("#9CA3AF"));

    private static readonly UiPalette Dark = new(
        SkyPlotBackground: Color.Parse("#1c2530"),
        SkyPlotBorder: Color.Parse("#5a6a7a"),
        SkyPlotRing30: Color.Parse("#333c4a"),
        SkyPlotRing60: Color.Parse("#2a3340"),
        SkyPlotMinElRing: Color.Parse("#56B4E9"),
        SkyPlotSpoke: Color.Parse("#4a5568"),
        SkyPlotLabel: Color.Parse("#c5d0dc"),
        SkyPlotMessage: Color.Parse("#9aa8b8"),
        MapFallbackBackground: Color.Parse("#1a2634"),
        MapLabelBackground: Color.FromArgb(220, 16, 20, 28),
        MapLabelForeground: Colors.White,
        GroundStationFill: Color.Parse("#56B4E9"),
        GroundStationOutlineDark: Color.Parse("#0d1117"),
        SunlightTimeline: Color.Parse("#F5C842"),
        EclipseTimeline: Color.Parse("#5C6370"));

    public static UiPalette Current =>
        Application.Current?.ActualThemeVariant == ThemeVariant.Dark ? Dark : Light;
}
