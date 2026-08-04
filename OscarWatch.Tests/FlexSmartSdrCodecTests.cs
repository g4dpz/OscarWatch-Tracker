using OscarWatch.Core.Radio;

namespace OscarWatch.Tests;

public class FlexSmartSdrCodecTests
{
    [Fact]
    public void BuildCommand_FormatsSequenceAndBody()
    {
        var cmd = FlexSmartSdrCodec.BuildCommand(12, "slice tune 0 145.9");
        Assert.Equal("C12|slice tune 0 145.9\n", cmd);
    }

    [Fact]
    public void BuildCommand_DebugPrefix()
    {
        var cmd = FlexSmartSdrCodec.BuildCommand(1, "ping", debug: true);
        Assert.StartsWith("CD1|", cmd);
    }

    [Fact]
    public void HzMhz_RoundTrip()
    {
        Assert.Equal(145_900_000, FlexSmartSdrCodec.MhzToHz(145.9));
        Assert.Equal(145.9, FlexSmartSdrCodec.HzToMhz(145_900_000), 6);
    }

    [Fact]
    public void TryParseLine_VersionAndHandle()
    {
        Assert.True(FlexSmartSdrCodec.TryParseLine("V1.2.0.0", out var v));
        Assert.Equal(FlexSmartSdrMessageKind.Version, v.Kind);
        Assert.Equal("1.2.0.0", v.Body);

        Assert.True(FlexSmartSdrCodec.TryParseLine("H545A4ACD", out var h));
        Assert.Equal(FlexSmartSdrMessageKind.Handle, h.Kind);
        Assert.Equal("545A4ACD", h.Handle);
    }

    [Fact]
    public void TryParseLine_ResponseSuccess()
    {
        Assert.True(FlexSmartSdrCodec.TryParseLine("R3|0|0", out var msg));
        Assert.Equal(FlexSmartSdrMessageKind.Response, msg.Kind);
        Assert.Equal(3u, msg.Sequence);
        Assert.Equal(0u, msg.HexResponse);
        Assert.True(FlexSmartSdrCodec.IsSuccessResponse(msg));
        Assert.True(FlexSmartSdrCodec.TryParseSliceCreateIndex(msg.Body, out var idx));
        Assert.Equal(0, idx);
    }

    [Fact]
    public void TryParseLine_ResponseFailure()
    {
        Assert.True(FlexSmartSdrCodec.TryParseLine("R9|5000002C|Incorrect number of parameters", out var msg));
        Assert.False(FlexSmartSdrCodec.IsSuccessResponse(msg));
        Assert.Equal(0x5000002Cu, msg.HexResponse);
    }

    [Fact]
    public void BuildSliceTuneCommand_includes_autopan_zero()
    {
        var cmd = FlexSmartSdrCodec.BuildSliceTuneCommand(3, 0, 145.9);
        Assert.Equal("C3|slice tune 0 145.9 autopan=0\n", cmd);
    }

    [Fact]
    public void TryParseDisplayPanStatus_extracts_center()
    {
        const string body = "display pan 0x40000001 center=145.865000 bandwidth=0.384";

        Assert.True(FlexSmartSdrCodec.TryParseDisplayPanStatus(body, out var pan));
        Assert.Equal("0x40000001", pan.StreamId);
        Assert.Equal(145_865_000, pan.CenterHz);
    }

    [Fact]
    public void BuildDisplayPanCenterCommand_formats_center()
    {
        var cmd = FlexSmartSdrCodec.BuildDisplayPanCenterCommand(4, "0x40000000", 435.15);
        Assert.Equal("C4|display pan set 0x40000000 center=435.15 autocenter=0\n", cmd);
    }

    [Fact]
    public void BuildAntListCommand_formats_body()
    {
        Assert.Equal("C8|ant list\n", FlexSmartSdrCodec.BuildAntListCommand(8));
    }

    [Theory]
    [InlineData("ANT1,ANT2,RX_A,RX_B,XVTA,XVTB", new[] { "ANT1", "ANT2", "RX_A", "RX_B", "XVTA", "XVTB" })]
    [InlineData("ANT1, ANT2, XVTR", new[] { "ANT1", "ANT2", "XVTR" })]
    [InlineData("ANT1,ANT2|OK", new[] { "ANT1", "ANT2" })]
    public void TryParseAntList_parses_comma_separated_ports(string body, string[] expected)
    {
        Assert.True(FlexSmartSdrCodec.TryParseAntList(body, out var antennas));
        Assert.Equal(expected, antennas);
    }

    [Fact]
    public void TryParseAntList_rejects_empty()
    {
        Assert.False(FlexSmartSdrCodec.TryParseAntList("", out _));
        Assert.False(FlexSmartSdrCodec.TryParseAntList("   ", out _));
    }

    [Fact]
    public void TryParseSliceStatus_ExtractsFields()
    {
        const string body =
            "slice 1 in_use=1 RF_frequency=435.150000 mode=USB tx=1 active=0 pan=0x40000001 " +
            "fm_tone_mode=ctcss_tx fm_tone_value=67.0";

        Assert.True(FlexSmartSdrCodec.TryParseSliceStatus(body, out var slice));
        Assert.Equal(1, slice.Index);
        Assert.Equal(435_150_000, slice.FrequencyHz);
        Assert.Equal("USB", slice.Mode);
        Assert.True(slice.IsTransmit);
        Assert.Equal("ctcss_tx", slice.FmToneMode);
        Assert.Equal(67.0, slice.FmToneHz);
        Assert.Equal("0x40000001", slice.PanStreamId);
    }

    [Fact]
    public void TryParseRadioFullDuplex()
    {
        const string body =
            "radio slices=2 nickname=Test full_duplex_enabled=1 binaural_rx=0";
        Assert.True(FlexSmartSdrCodec.TryParseRadioFullDuplex(body, out var enabled));
        Assert.True(enabled);
    }

    [Fact]
    public void BuildSliceSetToneCommands_SeparateModeAndValue()
    {
        var on = FlexSmartSdrCodec.BuildSliceSetToneModeCommand(5, 1, toneOn: true);
        var value = FlexSmartSdrCodec.BuildSliceSetToneValueCommand(6, 1, 67.0);
        var off = FlexSmartSdrCodec.BuildSliceSetToneModeCommand(7, 1, toneOn: false);

        Assert.Equal("C5|slice s 1 fm_tone_mode=ctcss_tx\n", on);
        Assert.Equal("C6|slice s 1 fm_tone_value=67.0\n", value);
        Assert.Equal("C7|slice s 1 fm_tone_mode=off\n", off);
    }

    [Theory]
    [InlineData("USB", "USB")]
    [InlineData("FM", "FM")]
    [InlineData("FMN", "NFM")]
    [InlineData("NFM", "NFM")]
    [InlineData("DATA-USB", "DIGU")]
    [InlineData("DIGL", "DIGL")]
    [InlineData(null, null)]
    public void FlexModeMapper_Maps(string? input, string? expected) =>
        Assert.Equal(expected, FlexModeMapper.ToSmartSdrMode(input));

    [Fact]
    public void BuildSliceSetAntCommands_use_slice_set_rxant_and_txant()
    {
        var rx = FlexSmartSdrCodec.BuildSliceSetRxAntCommand(8, 0, "RX_A");
        var tx = FlexSmartSdrCodec.BuildSliceSetTxAntCommand(9, 1, "XVTR");

        Assert.Equal("C8|slice set 0 rxant=RX_A\n", rx);
        Assert.Equal("C9|slice set 1 txant=XVTR\n", tx);
    }

    [Fact]
    public void BuildSliceCreateCommand_includes_pan_when_provided()
    {
        var cmd = FlexSmartSdrCodec.BuildSliceCreateCommand(11, 145.95, "USB", ant: null, panStreamId: "0x40000001");
        Assert.Equal("C11|slice create freq=145.95 pan=0x40000001 mode=USB\n", cmd);
    }

    [Fact]
    public void BuildSliceRemoveCommand_uses_slice_remove()
    {
        var cmd = FlexSmartSdrCodec.BuildSliceRemoveCommand(12, 1);
        Assert.Equal("C12|slice remove 1\n", cmd);
    }

    [Fact]
    public void BuildSliceCreateCommand_includes_ant_when_provided()
    {
        var cmd = FlexSmartSdrCodec.BuildSliceCreateCommand(10, 145.9, "USB", "RX_B");
        Assert.Contains("ant=RX_B", cmd, StringComparison.Ordinal);
    }
}
