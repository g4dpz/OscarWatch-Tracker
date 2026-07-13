using OscarWatch.Rotator;

namespace OscarWatch.Tests;

public sealed class SaebrtCommandFormatterTests
{
    [Theory]
    [InlineData(120.5, 45.0, "AZ120.5 EL045.0 UP000 XXX DN000 XXX")]
    [InlineData(0, 0, "AZ000.0 EL000.0 UP000 XXX DN000 XXX")]
    [InlineData(370, 90, "AZ370.0 EL090.0 UP000 XXX DN000 XXX")]
    [InlineData(359.6, 0.4, "AZ359.6 EL000.4 UP000 XXX DN000 XXX")]
    public void FormatSetPosition_matches_hamlib_style(double az, double el, string expected)
    {
        Assert.Equal(expected, SaebrtCommandFormatter.FormatSetPosition(az, el));
    }

    [Theory]
    [InlineData(120.5, 45.0, "AZ120.5EL045.0")]
    [InlineData(0, 0, "AZ000.0EL000.0")]
    public void FormatCompactSetPosition_omits_spaces_between_axes(double az, double el, string expected)
    {
        Assert.Equal(expected, SaebrtCommandFormatter.FormatCompactSetPosition(az, el));
    }
}
