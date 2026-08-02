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

    [Theory]
    [InlineData(null, false)]
    [InlineData(-1.01, false)]
    [InlineData(-2.0, false)]
    [InlineData(-1.0, true)]
    [InlineData(0.0, true)]
    [InlineData(45.0, true)]
    public void IsElevationReportable_requires_at_least_minus_one(double? elevation, bool expected)
    {
        Assert.Equal(expected, SatelliteStatusReportFormatting.IsElevationReportable(elevation));
    }
}
