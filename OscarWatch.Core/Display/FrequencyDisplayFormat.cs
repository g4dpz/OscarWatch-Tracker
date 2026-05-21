using System.Globalization;

namespace OscarWatch.Core.Display;

public static class FrequencyDisplayFormat
{
    /// <summary>Fixed-width MHz string for monospace column alignment (e.g. " 145.9544 MHz").</summary>
    public static string FormatMHz(double kHz)
    {
        if (kHz <= 0)
            return "      —       ";

        var mhz = kHz / 1000.0;
        return $"{mhz.ToString("F4", CultureInfo.InvariantCulture),10} MHz";
    }

    public static string FormatDopplerKHz(double shiftKHz) =>
        $"Δ {shiftKHz,8:F3} kHz";

    public static string FormatCtcssHz(double hz) =>
        Math.Abs(hz % 1) < 0.05 ? $"{hz.ToString("F0", CultureInfo.InvariantCulture)} Hz"
            : $"{hz.ToString("F1", CultureInfo.InvariantCulture)} Hz";
}
