using OscarWatch.Rotator;

namespace OscarWatch.Tests;

public class EasyCommPositionParserTests
{
    [Theory]
    [InlineData("AZ120.5", "AZ", 120)]
    [InlineData("AZ120.5;", "AZ", 120)]
    [InlineData("EL45.0", "EL", 45)]
    [InlineData("EL09.5", "EL", 10)]
    [InlineData("AZ=180.0", "AZ", 180)]
    [InlineData("  az 270.3  ", "AZ", 270)]
    public void TryParseAxis_parses_common_controller_replies(string response, string axis, int expected)
    {
        Assert.Equal(expected, EasyCommPositionParser.TryParseAxis(response, axis));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("VE1.0")]
    [InlineData("not-a-number")]
    public void TryParseAxis_returns_null_for_invalid_replies(string? response)
    {
        Assert.Null(EasyCommPositionParser.TryParseAxis(response, "AZ"));
    }
}
