using Avalonia.Media;

namespace OscarWatch.Controls;

/// <summary>
/// Color-blind-safe satellite colors (Okabe–Ito base, extended for larger catalogs).
/// See docs/ACCESSIBILITY.md.
/// </summary>
internal static class PlotColors
{
    public static readonly Color[] Satellite =
    [
        Color.Parse("#E69F00"), // orange
        Color.Parse("#56B4E9"), // sky blue
        Color.Parse("#009E73"), // bluish green
        Color.Parse("#F0E442"), // yellow
        Color.Parse("#0072B2"), // blue
        Color.Parse("#D55E00"), // vermillion
        Color.Parse("#CC79A7"), // reddish purple
        Color.Parse("#882255"), // wine
        Color.Parse("#44AA99"), // teal
        Color.Parse("#999933"), // olive
        Color.Parse("#661100"), // brown
        Color.Parse("#332288"), // indigo
    ];

    public static Color ForIndex(int index) => Satellite[index % Satellite.Length];
}
