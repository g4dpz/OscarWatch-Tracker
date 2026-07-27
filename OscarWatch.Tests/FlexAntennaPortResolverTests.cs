using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;

namespace OscarWatch.Tests;

public class FlexAntennaPortResolverTests
{
    [Theory]
    [InlineData(145_900_000, "RXB")]
    [InlineData(435_300_000, "RXA")]
    public void ResolveRxPort_uses_band_specific_settings(long hz, string expected)
    {
        var settings = new RigSettings
        {
            FlexVhfRxAnt = "RXB",
            FlexUhfRxAnt = "RXA"
        };

        Assert.Equal(expected, FlexAntennaPortResolver.ResolveRxPort(settings, hz));
    }

    [Theory]
    [InlineData(145_800_000, "XVTR")]
    [InlineData(435_000_000, "ANT1")]
    public void ResolveTxPort_uses_band_specific_settings(long hz, string expected)
    {
        var settings = new RigSettings
        {
            FlexVhfTxAnt = "XVTR",
            FlexUhfTxAnt = "ANT1"
        };

        Assert.Equal(expected, FlexAntennaPortResolver.ResolveTxPort(settings, hz));
    }

    [Fact]
    public void ResolvePorts_return_null_when_unconfigured()
    {
        var settings = new RigSettings();
        Assert.Null(FlexAntennaPortResolver.ResolveRxPort(settings, 145_900_000));
        Assert.Null(FlexAntennaPortResolver.ResolveTxPort(settings, 435_000_000));
    }

    [Fact]
    public void NormalizeToken_rejects_unknown_values()
    {
        Assert.Equal("RXA", FlexAntennaPortResolver.NormalizeToken("rxa"));
        Assert.Null(FlexAntennaPortResolver.NormalizeToken("INVALID"));
        Assert.Null(FlexAntennaPortResolver.NormalizeToken(""));
    }
}
