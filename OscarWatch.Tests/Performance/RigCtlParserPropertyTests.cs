// Feature: startup-io-rendering-optimisation, Property 11: RigCtl span-based parser equivalence

using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Rig;

namespace OscarWatch.Tests.Performance;

/// <summary>
/// Property-based tests for RigCtlResponseParser span-based parsing.
/// Verifies the parser produces correct results for generated rigctl responses.
///
/// **Validates: Requirements 9.3**
/// </summary>
public sealed class RigCtlParserPropertyTests
{
    /// <summary>
    /// Property 11: For any valid frequency value formatted as a rigctl response,
    /// TryParseFrequencyHz SHALL return the original frequency value.
    ///
    /// **Validates: Requirements 9.3**
    /// </summary>
    [Property(MaxTest = 200)]
    public bool Frequency_roundtrip_preserves_value(ulong rawHz)
    {
        // Constrain to realistic amateur radio frequency range (100 kHz to 50 GHz)
        var hz = (long)(rawHz % 50_000_000_000UL) + 100_000;
        var response = $"{hz}\nRPRT 0\n";

        var parsed = RigCtlResponseParser.TryParseFrequencyHz(response);

        return parsed == hz;
    }

    /// <summary>
    /// Property 11: For any valid frequency value as a single line (no RPRT),
    /// TryParseFrequencyHz SHALL return the frequency.
    ///
    /// **Validates: Requirements 9.3**
    /// </summary>
    [Property(MaxTest = 200)]
    public bool Frequency_single_line_parses_correctly(ulong rawHz)
    {
        var hz = (long)(rawHz % 50_000_000_000UL) + 100_000;
        var response = $"{hz}\n";

        var parsed = RigCtlResponseParser.TryParseFrequencyHz(response);

        return parsed == hz;
    }

    /// <summary>
    /// Property 11: For any RPRT code, IsSuccess SHALL return true only when code is 0.
    ///
    /// **Validates: Requirements 9.3**
    /// </summary>
    [Property(MaxTest = 200)]
    public bool IsSuccess_returns_true_only_for_code_zero(int code)
    {
        var response = $"RPRT {code}\n";

        var result = RigCtlResponseParser.IsSuccess(response);

        return result == (code == 0);
    }

    /// <summary>
    /// Property 11: For any frequency response with whitespace padding (spaces, \r\n),
    /// the parser SHALL still extract the correct value.
    ///
    /// **Validates: Requirements 9.3**
    /// </summary>
    [Property(MaxTest = 200)]
    public bool Frequency_with_whitespace_padding_parses_correctly(ulong rawHz, byte paddingByte)
    {
        var hz = (long)(rawHz % 50_000_000_000UL) + 100_000;
        var leadingSpaces = new string(' ', (paddingByte % 4));
        var trailingSpaces = new string(' ', (paddingByte % 3));
        var response = $"{leadingSpaces}{hz}{trailingSpaces}\r\n";

        var parsed = RigCtlResponseParser.TryParseFrequencyHz(response);

        return parsed == hz;
    }

    /// <summary>
    /// Property 11: LooksComplete returns true for any response ending with
    /// a valid RPRT line or a numeric frequency line terminated by newline.
    ///
    /// **Validates: Requirements 9.3**
    /// </summary>
    [Property(MaxTest = 200)]
    public bool LooksComplete_detects_complete_rprt_responses(int code)
    {
        var response = $"RPRT {code}\n";
        return RigCtlResponseParser.LooksComplete(response);
    }

    /// <summary>
    /// Property 11: LooksComplete returns true for numeric-only responses.
    ///
    /// **Validates: Requirements 9.3**
    /// </summary>
    [Property(MaxTest = 200)]
    public bool LooksComplete_detects_complete_frequency_responses(ulong rawHz)
    {
        var hz = (long)(rawHz % 50_000_000_000UL) + 100_000;
        var response = $"{hz}\n";
        return RigCtlResponseParser.LooksComplete(response);
    }

    // --- Unit tests for specific examples ---

    [Fact]
    public void IsSuccess_with_RPRT_0_returns_true()
    {
        Assert.True(RigCtlResponseParser.IsSuccess("RPRT 0\n"));
    }

    [Fact]
    public void IsSuccess_with_RPRT_1_returns_false()
    {
        Assert.False(RigCtlResponseParser.IsSuccess("RPRT 1\n"));
    }

    [Fact]
    public void TryParseFrequencyHz_parses_typical_frequency()
    {
        Assert.Equal(435_850_000L, RigCtlResponseParser.TryParseFrequencyHz("435850000\n"));
    }

    [Fact]
    public void TryParseFrequencyHz_parses_frequency_with_rprt()
    {
        Assert.Equal(145_920_000L, RigCtlResponseParser.TryParseFrequencyHz("145920000\nRPRT 0\n"));
    }

    [Fact]
    public void TryParseFrequencyHz_returns_null_for_empty()
    {
        Assert.Null(RigCtlResponseParser.TryParseFrequencyHz(""));
    }

    [Fact]
    public void LooksComplete_returns_false_for_empty()
    {
        Assert.False(RigCtlResponseParser.LooksComplete(""));
    }

    [Fact]
    public void LooksComplete_returns_false_for_partial_response()
    {
        Assert.False(RigCtlResponseParser.LooksComplete("RPR"));
    }

    [Fact]
    public void TryParseFrequencyHz_parses_floating_point()
    {
        // Some rigctl implementations return frequency as a float
        Assert.Equal(435_850_000L, RigCtlResponseParser.TryParseFrequencyHz("435850000.0\n"));
    }
}
