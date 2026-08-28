using OscarWatch.Rig;

namespace OscarWatch.Tests;

public sealed class FlexSmartSdrClientParsePresentFieldsTests
{
    [Fact]
    public void Parses_typical_slice_status_fields()
    {
        var body = "slice 0 in_use=1 RF_frequency=14.200000 mode=USB tx=0 active=1";
        var fields = FlexSmartSdrClient.ParsePresentFields(body);

        Assert.Contains("in_use", fields);
        Assert.Contains("RF_frequency", fields);
        Assert.Contains("mode", fields);
        Assert.Contains("tx", fields);
        Assert.Contains("active", fields);
        Assert.DoesNotContain("freq", fields);
        Assert.DoesNotContain("fm_tone_mode", fields);
    }

    [Fact]
    public void Parses_fm_tone_fields()
    {
        var body = "slice 1 fm_tone_mode=ctcss_tx fm_tone_value=67.0";
        var fields = FlexSmartSdrClient.ParsePresentFields(body);

        Assert.Contains("fm_tone_mode", fields);
        Assert.Contains("fm_tone_value", fields);
        Assert.DoesNotContain("in_use", fields);
    }

    [Fact]
    public void Case_insensitive_lookup()
    {
        var body = "slice 0 MODE=USB TX=1";
        var fields = FlexSmartSdrClient.ParsePresentFields(body);

        Assert.Contains("MODE", fields);
        Assert.Contains("mode", fields);
        Assert.Contains("TX", fields);
        Assert.Contains("tx", fields);
    }

    [Fact]
    public void Ignores_tokens_without_equals()
    {
        var body = "slice 0 in_use=1 sometoken mode=FM";
        var fields = FlexSmartSdrClient.ParsePresentFields(body);

        Assert.Contains("in_use", fields);
        Assert.Contains("mode", fields);
        Assert.DoesNotContain("sometoken", fields);
        Assert.DoesNotContain("slice", fields);
    }

    [Fact]
    public void Empty_body_returns_empty_set()
    {
        var fields = FlexSmartSdrClient.ParsePresentFields("");
        Assert.Empty(fields);
    }

    [Fact]
    public void Handles_extra_whitespace()
    {
        var body = "  slice  0  freq=145.900   tx=1  ";
        var fields = FlexSmartSdrClient.ParsePresentFields(body);

        Assert.Contains("freq", fields);
        Assert.Contains("tx", fields);
    }
}
