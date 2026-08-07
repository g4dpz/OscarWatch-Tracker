using OscarWatch.Core.Radio;

namespace OscarWatch.Tests;

public sealed class KenwoodThD7xCatCodecTests
{
    [Fact]
    public void Commands_use_band_b_space_and_cr_framing()
    {
        Assert.Equal("VM 1,0\r", KenwoodThD7xCatCodec.BuildVfoModeCommand());
        Assert.Equal("BC 1\r", KenwoodThD7xCatCodec.BuildControlBandCommand());
        Assert.Equal("FO 1\r", KenwoodThD7xCatCodec.BuildReadFrequencyCommand());
        Assert.Equal("FQ 1,0145745000\r", KenwoodThD7xCatCodec.BuildSetFrequencyCommand(145_745_000));
        Assert.Equal("MD 1,4\r", KenwoodThD7xCatCodec.BuildSetModeCommand("USB"));
    }

    [Theory]
    [InlineData("FM", '6', false)]
    [InlineData("FMN", '6', false)]
    [InlineData("AM", '2', true)]
    [InlineData("LSB", '3', true)]
    [InlineData("USB", '4', true)]
    [InlineData("CW", '5', true)]
    [InlineData("CWR", '9', true)]
    public void Mode_mapping_matches_thd75_measurements(string mode, char expected, bool fine)
    {
        Assert.Equal(expected, KenwoodThD7xCatCodec.ResolveModeCode(mode));
        Assert.Equal(fine, KenwoodThD7xCatCodec.UsesFineTuning(mode));
    }

    [Fact]
    public void Frequency_rounding_uses_5khz_for_fm_and_20hz_for_linear()
    {
        Assert.Equal(145_745_000, KenwoodThD7xCatCodec.RoundFrequencyToStep(145_743_100, false));
        Assert.Equal(145_743_100, KenwoodThD7xCatCodec.RoundFrequencyToStep(145_743_101, true));
        Assert.Equal(145_743_120, KenwoodThD7xCatCodec.RoundFrequencyToStep(145_743_111, true));
    }

    [Fact]
    public void Parses_fo_frequency_record()
    {
        Assert.True(KenwoodThD7xCatCodec.TryParseFrequencyHz("FO 1,0145745000,0,0,0,0\r", out var hz));
        Assert.Equal(145_745_000, hz);
    }
}
