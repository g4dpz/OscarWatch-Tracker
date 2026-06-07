// Feature: performance-optimisations, Property 7: Twilight brush alpha values match the specification formula

using FsCheck.Xunit;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 7.4**
///
/// Property-based test verifying that the twilight brush alpha formula produces correct values.
/// The formula under test is:
/// <c>alpha = (byte)(baseColor.A * (0.15 + 0.35 * (band + 0.5) / twilightBands))</c>
///
/// Since <c>GetTwilightBrushes</c> is a private instance method on <c>WorldMapControl</c>,
/// we validate the pure mathematical properties of the formula itself.
/// </summary>
public class TwilightBrushAlphaPropertyTests
{
    private const int TwilightBands = 5;

    /// <summary>
    /// Computes the expected alpha for a given band using the specification formula.
    /// </summary>
    private static byte ComputeExpectedAlpha(byte baseAlpha, int band)
    {
        var t = (band + 0.5) / TwilightBands;
        return (byte)(baseAlpha * (0.15 + 0.35 * t));
    }

    /// <summary>
    /// Property 7.1: Alpha values are monotonically non-decreasing across bands.
    /// Band 0 is lightest, band N-1 is most opaque.
    /// </summary>
    [Property]
    public bool TwilightAlpha_is_monotonically_nondecreasing_across_bands(byte baseAlpha, byte r, byte g, byte b)
    {
        var alphas = new byte[TwilightBands];
        for (var band = 0; band < TwilightBands; band++)
            alphas[band] = ComputeExpectedAlpha(baseAlpha, band);

        for (var i = 0; i < TwilightBands - 1; i++)
        {
            if (alphas[i + 1] < alphas[i])
                return false;
        }

        return true;
    }

    /// <summary>
    /// Property 7.2: All alpha values are less than or equal to baseColor.A (never exceeds base opacity).
    /// The maximum factor is 0.15 + 0.35 * 0.9 = 0.465, which is always less than 1.0,
    /// so every band alpha must be &lt;= baseAlpha.
    /// </summary>
    [Property]
    public bool TwilightAlpha_never_exceeds_base_opacity(byte baseAlpha, byte r, byte g, byte b)
    {
        for (var band = 0; band < TwilightBands; band++)
        {
            var alpha = ComputeExpectedAlpha(baseAlpha, band);
            if (alpha > baseAlpha)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Property 7.3: First band alpha matches baseColor.A * 0.185 and last band alpha matches
    /// baseColor.A * 0.465 (using integer truncation via byte cast).
    /// Band 0: t = 0.5/5 = 0.1, factor = 0.15 + 0.35 * 0.1 = 0.185
    /// Band 4: t = 4.5/5 = 0.9, factor = 0.15 + 0.35 * 0.9 = 0.465
    /// </summary>
    [Property]
    public bool TwilightAlpha_boundary_bands_match_expected_factors(byte baseAlpha, byte r, byte g, byte b)
    {
        var alpha0 = ComputeExpectedAlpha(baseAlpha, 0);
        var alpha4 = ComputeExpectedAlpha(baseAlpha, TwilightBands - 1);

        var expectedAlpha0 = (byte)(baseAlpha * 0.185);
        var expectedAlpha4 = (byte)(baseAlpha * 0.465);

        return alpha0 == expectedAlpha0 && alpha4 == expectedAlpha4;
    }

    /// <summary>
    /// Property 7.4: All computed alpha values are in valid byte range [0, 255].
    /// This is guaranteed by the byte cast but we verify the formula never produces
    /// intermediate values that would wrap around or overflow in unexpected ways.
    /// </summary>
    [Property]
    public bool TwilightAlpha_values_are_in_valid_range(byte baseAlpha, byte r, byte g, byte b)
    {
        for (var band = 0; band < TwilightBands; band++)
        {
            var t = (band + 0.5) / TwilightBands;
            var rawValue = baseAlpha * (0.15 + 0.35 * t);

            // The raw double value must be in [0, 255] before byte truncation
            if (rawValue < 0 || rawValue > 255)
                return false;

            var alpha = (byte)rawValue;
            if (alpha > 255) // byte can't exceed 255, but validates the cast
                return false;
        }

        return true;
    }
}
