using OscarWatch.Rotator;

namespace OscarWatch.Tests;

public sealed class SaebrtPositionParserTests
{
    [Theory]
    [InlineData("AZ120.5 EL45.0", 120, 45)]
    [InlineData("AZ120.5EL45.0", 120, 45)]
    [InlineData("  az000.0 el090.0  ", 0, 90)]
    [InlineData("AZ370.0 EL180.0", 370, 180)]
    public void TryParseCombined_parses_spaced_and_compact_replies(string response, int az, int el)
    {
        Assert.True(SaebrtPositionParser.TryParseCombined(response, out var parsedAz, out var parsedEl));
        Assert.Equal(az, parsedAz);
        Assert.Equal(el, parsedEl);
    }

    [Theory]
    [InlineData("AZ120.5", 120)]
    [InlineData("EL45.0", 45)]
    public void TryParseAxis_parses_single_axis_replies(string response, int expected)
    {
        var axis = response.StartsWith("EL", StringComparison.OrdinalIgnoreCase) ? "EL" : "AZ";
        Assert.Equal(expected, SaebrtPositionParser.TryParseAxis(response, axis));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("UP000")]
    [InlineData("not-a-position")]
    public void TryParseCombined_returns_false_for_invalid_replies(string? response)
    {
        Assert.False(SaebrtPositionParser.TryParseCombined(response, out _, out _));
    }
}
