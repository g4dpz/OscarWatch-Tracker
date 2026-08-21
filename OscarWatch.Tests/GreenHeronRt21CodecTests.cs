using OscarWatch.Rotator;

namespace OscarWatch.Tests;

public sealed class GreenHeronRt21CodecTests
{
    [Theory]
    [InlineData(45.0, "AP1045.0\r;")]
    [InlineData(9.5, "AP1009.5\r;")]
    [InlineData(370.0, "AP1370.0\r;")]
    [InlineData(90.0, "AP1090.0\r;")]
    [InlineData(0.0, "AP1000.0\r;")]
    public void FormatSetPosition_pads_to_five_chars_with_tenths(double heading, string expected) =>
        Assert.Equal(expected, GreenHeronRt21Codec.FormatSetPosition(heading));

    [Fact]
    public void FormatQueryTenths_is_BI1() =>
        Assert.Equal("BI1;", GreenHeronRt21Codec.FormatQueryTenths());

    [Fact]
    public void FormatStop_is_semicolon() =>
        Assert.Equal(";", GreenHeronRt21Codec.FormatStop());

    [Theory]
    [InlineData("045.0;", 45.0)]
    [InlineData(" 45.0;", 45.0)]
    [InlineData("359.9;", 359.9)]
    [InlineData("000;", 0.0)]
    [InlineData("000.0", 0.0)]
    [InlineData("360;", 0.0)]
    [InlineData("370.0;", 370.0)]
    public void TryParseHeading_accepts_common_replies(string response, double expected)
    {
        Assert.True(GreenHeronRt21Codec.TryParseHeading(response, out var heading));
        Assert.Equal(expected, heading, 3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc;")]
    [InlineData("500.0;")]
    public void TryParseHeading_rejects_invalid(string? response) =>
        Assert.False(GreenHeronRt21Codec.TryParseHeading(response, out _));

    [Fact]
    public void ToDisplayDegrees_rounds()
    {
        Assert.Equal(46, GreenHeronRt21Codec.ToDisplayDegrees(45.6));
        Assert.Equal(45, GreenHeronRt21Codec.ToDisplayDegrees(45.4));
    }
}
