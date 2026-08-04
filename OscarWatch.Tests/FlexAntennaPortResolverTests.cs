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
    [InlineData(145_800_000, "XVTA")]
    [InlineData(435_000_000, "XVTB")]
    public void ResolveTxPort_uses_xvta_xvtb(long hz, string expected)
    {
        var settings = new RigSettings
        {
            FlexVhfTxAnt = "XVTA",
            FlexUhfTxAnt = "XVTB"
        };

        Assert.Equal(expected, FlexAntennaPortResolver.ResolveTxPort(settings, hz));
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
    public void NormalizeToken_maps_legacy_and_xvtr_aliases()
    {
        Assert.Equal("RX_A", FlexAntennaPortResolver.NormalizeToken("rxa"));
        Assert.Equal("RX_B", FlexAntennaPortResolver.NormalizeToken("RXB"));
        Assert.Equal("RX_A", FlexAntennaPortResolver.NormalizeToken("RX_A"));
        Assert.Equal("XVTA", FlexAntennaPortResolver.NormalizeToken("xvtr_a"));
        Assert.Equal("XVTA", FlexAntennaPortResolver.NormalizeToken("XVTRA"));
        Assert.Equal("XVTB", FlexAntennaPortResolver.NormalizeToken("XVTRB"));
        Assert.Equal("XVTB", FlexAntennaPortResolver.NormalizeToken("xvtr_b"));
        Assert.Equal("XVTA", FlexAntennaPortResolver.NormalizeToken("XVTA"));
        Assert.Null(FlexAntennaPortResolver.NormalizeToken("INVALID TOKEN"));
        Assert.Null(FlexAntennaPortResolver.NormalizeToken(""));
    }

    [Fact]
    public void NormalizeToken_accepts_radio_reported_future_ports()
    {
        Assert.Equal("XVTC", FlexAntennaPortResolver.NormalizeToken("xvtC"));
        Assert.Equal("ANT3", FlexAntennaPortResolver.NormalizeToken("ANT3"));
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

    [Fact]
    public void FormatDisplayLabel_uses_operator_friendly_names()
    {
        Assert.Equal("RX A", FlexAntennaPortResolver.FormatDisplayLabel("RX_A"));
        Assert.Equal("XVTR A", FlexAntennaPortResolver.FormatDisplayLabel("XVTA"));
        Assert.Equal("XVTR B", FlexAntennaPortResolver.FormatDisplayLabel("XVTB"));
        Assert.Equal("ANT1", FlexAntennaPortResolver.FormatDisplayLabel("ANT1"));
    }

    [Fact]
    public void MergeAntennaTokens_puts_radio_ports_first_then_baseline()
    {
        var merged = FlexAntennaPortResolver.MergeAntennaTokens(["XVTA", "XVTB", "ANT1"]);
        Assert.Equal(["XVTA", "XVTB", "ANT1", "ANT2", "RX_A", "RX_B", "XVTR"], merged);
    }

    [Fact]
    public void MergeAntennaTokens_null_radio_returns_baseline()
    {
        var merged = FlexAntennaPortResolver.MergeAntennaTokens(null);
        Assert.Equal(FlexAntennaPortResolver.KnownTokens, merged);
    }

    [Fact]
    public void MergeAntennaTokens_keeps_unknown_radio_ports()
    {
        var merged = FlexAntennaPortResolver.MergeAntennaTokens(["ANT1", "XVTC"]);
        Assert.Contains("XVTC", merged);
        Assert.Contains("XVTA", merged);
    }
}
