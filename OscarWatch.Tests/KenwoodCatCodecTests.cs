using OscarWatch.Core.Radio;

namespace OscarWatch.Tests;

public sealed class KenwoodCatCodecTests
{
    [Fact]
    public void BuildSetFrequencyCommand_formats_11_digit_hz()
    {
        var cmd = KenwoodCatCodec.BuildSetFrequencyCommand('A', 435_750_000);
        Assert.Equal("FA00435750000;", cmd);
    }

    [Fact]
    public void TryParseFrequencyHz_reads_reply()
    {
        Assert.True(KenwoodCatCodec.TryParseFrequencyHz("FA00435750000;", out var hz));
        Assert.Equal(435_750_000, hz);
    }

    [Fact]
    public void TryParseSatelliteOn_detects_sat_flag()
    {
        Assert.True(KenwoodCatCodec.TryParseSatelliteOn("SA1;"));
        Assert.False(KenwoodCatCodec.TryParseSatelliteOn("SA0;"));
    }

    [Fact]
    public void TryGetModeCode_maps_usb_and_fm()
    {
        Assert.True(KenwoodCatCodec.TryGetModeCode("USB", out var usb));
        Assert.Equal('2', usb);
        Assert.True(KenwoodCatCodec.TryGetModeCode("FM", out var fm));
        Assert.Equal('4', fm);
    }

    [Fact]
    public void TryGetCtcssIndex_maps_67Hz()
    {
        Assert.True(KenwoodCatCodec.TryGetCtcssIndex(67.0, out var index));
        Assert.Equal(1, index);
        Assert.Equal("CN01;", KenwoodCatCodec.BuildCtcssFrequencyCommand(index));
    }

}
