using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

public sealed class SatelliteStatusReportFormattingTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    [InlineData("jo", null)]
    [InlineData("jo01", "JO01")]
    [InlineData("jo01uk", "JO01UK")]
    [InlineData("JO01UK12", "JO01UK")]
    public void NormalizeGridsquare_returns_4_or_6_char_upper(string? input, string? expected)
    {
        Assert.Equal(expected, SatelliteStatusReportFormatting.NormalizeGridsquare(input));
    }
}
