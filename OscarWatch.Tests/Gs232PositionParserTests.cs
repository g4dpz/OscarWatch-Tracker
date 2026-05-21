using OscarWatch.Rotator;

namespace OscarWatch.Tests;

public sealed class Gs232PositionParserTests
{
    [Theory]
    [InlineData("AZ=120 EL=45", 120, 45)]
    [InlineData("AZ=000 EL=000", 0, 0)]
    [InlineData("  AZ=90   EL=10  ", 90, 10)]
    public void TryParseCombined_parses_az_el_prefixes(string response, int az, int el)
    {
        Assert.True(Gs232PositionParser.TryParseCombined(response, out var parsedAz, out var parsedEl));
        Assert.Equal(az, parsedAz);
        Assert.Equal(el, parsedEl);
    }

    [Theory]
    [InlineData("AZ=120", 120, null)]
    [InlineData("EL=45", null, 45)]
    public void TryParseParts_accepts_single_axis_from_c2(string response, int? az, int? el)
    {
        Gs232PositionParser.TryParseParts(response, out var parsedAz, out var parsedEl);
        Assert.Equal(az, parsedAz);
        Assert.Equal(el, parsedEl);
        Assert.False(Gs232PositionParser.TryParseCombined(response, out _, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("000 000")]
    [InlineData("W120 045")]
    [InlineData("C2")]
    public void TryParseParts_rejects_non_position_lines(string response)
    {
        Gs232PositionParser.TryParseParts(response, out var az, out var el);
        Assert.Null(az);
        Assert.Null(el);
        Assert.False(Gs232PositionParser.TryParseCombined(response, out _, out _));
    }

    [Fact]
    public void TryParseAzimuthLine_requires_prefix()
    {
        Assert.True(Gs232PositionParser.TryParseAzimuthLine("AZ=123", out var az));
        Assert.Equal(123, az);
        Assert.False(Gs232PositionParser.TryParseAzimuthLine("123", out _));
        Assert.False(Gs232PositionParser.TryParseAzimuthLine("000", out _));
    }
}
