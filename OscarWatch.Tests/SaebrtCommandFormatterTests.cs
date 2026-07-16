using OscarWatch.Rotator;

namespace OscarWatch.Tests;

public sealed class SaebrtCommandFormatterTests
{
    [Theory]
    [InlineData(120.5, 45.0, "AZ121EL045")]
    [InlineData(0, 0, "AZ000EL000")]
    [InlineData(370, 90, "AZ370EL090")]
    [InlineData(359.6, 0.4, "AZ360EL000")]
    [InlineData(250, 6, "AZ250EL006")]
    public void FormatSetPosition_uses_compact_whole_degrees(double az, double el, string expected)
    {
        Assert.Equal(expected, SaebrtCommandFormatter.FormatSetPosition(az, el));
    }
}
