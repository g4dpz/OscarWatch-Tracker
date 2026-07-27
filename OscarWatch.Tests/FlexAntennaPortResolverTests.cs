using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;

namespace OscarWatch.Tests;

public class FlexAntennaPortResolverTests
{
    [Theory]
    [InlineData(145_900_000, "RX_B")]
    [InlineData(435_300_000, "RX_A")]
    public void ResolveRxPort_uses_band_specific_settings(long hz, string expected)
    {
        var settings = new RigSettings
        {
            FlexVhfRxAnt = "RX_B",
            FlexUhfRxAnt = "RX_A"
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
    public void NormalizeToken_maps_legacy_rxa_rxb_to_underscore_form()
    {
        Assert.Equal("RX_A", FlexAntennaPortResolver.NormalizeToken("rxa"));
        Assert.Equal("RX_B", FlexAntennaPortResolver.NormalizeToken("RXB"));
        Assert.Equal("RX_A", FlexAntennaPortResolver.NormalizeToken("RX_A"));
        Assert.Null(FlexAntennaPortResolver.NormalizeToken("INVALID"));
        Assert.Null(FlexAntennaPortResolver.NormalizeToken(""));
    }

    [Fact]
    public void ResolveRxPort_accepts_legacy_saved_tokens()
    {
        var settings = new RigSettings
        {
            FlexVhfRxAnt = "RXB",
            FlexUhfRxAnt = "RXA"
        };

        Assert.Equal("RX_B", FlexAntennaPortResolver.ResolveRxPort(settings, 145_900_000));
        Assert.Equal("RX_A", FlexAntennaPortResolver.ResolveRxPort(settings, 435_300_000));
    }
}
