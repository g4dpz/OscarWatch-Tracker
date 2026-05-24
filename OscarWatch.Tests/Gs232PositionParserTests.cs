using OscarWatch.Rotator;

namespace OscarWatch.Tests;

public sealed class Gs232PositionParserTests
{
    [Theory]
    [InlineData("AZ=120 EL=45", 120, 45)]
    [InlineData("AZ=000 EL=000", 0, 0)]
    [InlineData("  AZ=90   EL=10  ", 90, 10)]
    public void TryParseCombined_parses_gs232b_az_el(string response, int az, int el)
    {
        Assert.True(Gs232PositionParser.TryParseCombined(response, out var parsedAz, out var parsedEl));
        Assert.Equal(az, parsedAz);
        Assert.Equal(el, parsedEl);
    }

    [Theory]
    [InlineData("+0120+0045", 120, 45)]
    [InlineData("+0450+0180", 450, 180)]
    [InlineData("+0000+0000", 0, 0)]
    public void TryParseCombined_parses_gs232a_c2_concatenated(string response, int az, int el)
    {
        Assert.True(Gs232PositionParser.TryParseCombined(response, out var parsedAz, out var parsedEl));
        Assert.Equal(az, parsedAz);
        Assert.Equal(el, parsedEl);
    }

    [Theory]
    [InlineData("+0120 +0045", 120, 45)]
    public void TryParseCombined_parses_gs232a_spaced_plus_zero_tokens(string response, int az, int el)
    {
        Assert.True(Gs232PositionParser.TryParseCombined(response, out var parsedAz, out var parsedEl));
        Assert.Equal(az, parsedAz);
        Assert.Equal(el, parsedEl);
    }

    [Theory]
    [InlineData("AZ=120", 120, null)]
    [InlineData("EL=45", null, 45)]
    [InlineData("+0120", 120, null)]
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

    [Theory]
    [InlineData("AZ=123", 123)]
    [InlineData("+0123", 123)]
    [InlineData("+0450", 450)]
    public void TryParseAzimuthLine_accepts_gs232b_and_gs232a(string response, int az)
    {
        Assert.True(Gs232PositionParser.TryParseAzimuthLine(response, out var parsed));
        Assert.Equal(az, parsed);
    }

    [Theory]
    [InlineData("EL=45", 45)]
    [InlineData("+0045", 45)]
    [InlineData("+0180", 180)]
    public void TryParseElevationLine_accepts_gs232b_and_gs232a(string response, int el)
    {
        Assert.True(Gs232PositionParser.TryParseElevationLine(response, out var parsed));
        Assert.Equal(el, parsed);
    }

    [Fact]
    public void TryParseAzimuthLine_rejects_bare_digits()
    {
        Assert.False(Gs232PositionParser.TryParseAzimuthLine("123", out _));
        Assert.False(Gs232PositionParser.TryParseAzimuthLine("000", out _));
    }
}
