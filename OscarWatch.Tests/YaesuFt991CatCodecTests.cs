using OscarWatch.Core.Radio;

namespace OscarWatch.Tests;

public sealed class YaesuFt991CatCodecTests
{
    [Fact]
    public void BuildSetFrequencyCommand_uses_nine_digit_hz()
    {
        Assert.Equal("FA014250000;", YaesuFt991CatCodec.BuildSetFrequencyCommand(vfoB: false, 14_250_000));
    }

    [Fact]
    public void TryParseFrequencyHz_reads_fa_response()
    {
        Assert.True(YaesuFt991CatCodec.TryParseFrequencyHz("FA014250000;", out var hz));
        Assert.Equal(14_250_000, hz);
    }

    [Fact]
    public void TryGetCtcssIndex_67_hz_is_zero()
    {
        Assert.True(YaesuFt991CatCodec.TryGetCtcssIndex(67.0, out var index));
        Assert.Equal(0, index);
        Assert.Equal("CN00000;CT02;", YaesuFt991CatCodec.BuildCtcssEncodeCommand(index));
    }

    [Fact]
    public void TryGetModeCode_maps_fm_and_usb()
    {
        Assert.True(YaesuFt991CatCodec.TryGetModeCode("FM", out var fm));
        Assert.Equal('4', fm);
        Assert.Equal("MD04;", YaesuFt991CatCodec.BuildSetModeCommand(fm));

        Assert.True(YaesuFt991CatCodec.TryGetModeCode("USB", out var usb));
        Assert.Equal('2', usb);
    }
}
