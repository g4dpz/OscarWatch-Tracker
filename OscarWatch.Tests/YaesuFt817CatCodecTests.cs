using OscarWatch.Core.Radio;

namespace OscarWatch.Tests;

public sealed class YaesuFt817CatCodecTests
{
    [Fact]
    public void DecodeFrequency_round_trip()
    {
        var response = new byte[] { 0x14, 0x25, 0x00, 0x00, 0x01 };
        var hz = YaesuFt817CatCodec.DecodeFrequency10Hz(response);
        Assert.Equal(142_500_000, hz);

        var cmd = YaesuFt817CatCodec.BuildSetFrequencyCommand(hz);
        Assert.Equal(142_500_000, YaesuFt817CatCodec.DecodeFrequency10Hz(cmd));
        Assert.Equal(0x01, cmd[4]);
    }

    [Fact]
    public void BuildSetMode_FM_uses_wide_byte()
    {
        var cmd = YaesuFt817CatCodec.BuildSetModeCommand("FM");
        Assert.Equal(0x08, cmd[0]);
        Assert.Equal(0x07, cmd[4]);
    }

    [Fact]
    public void BuildCtcss_67Hz()
    {
        Assert.True(YaesuFt817CatCodec.TryGetCtcssCatCode(67.0, out var code));
        var cmd = YaesuFt817CatCodec.BuildCtcssFrequencyCommand(67.0);
        Assert.Equal(code, cmd[0]);
        Assert.Equal(0x0b, cmd[4]);
    }

    [Fact]
    public void Split_commands()
    {
        Assert.Equal(0x02, YaesuFt817CatCodec.SplitOn[4]);
        Assert.Equal(0x82, YaesuFt817CatCodec.SplitOff[4]);
    }
}
