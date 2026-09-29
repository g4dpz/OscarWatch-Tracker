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
        Assert.True(slice.InUse);
    }

    [Fact]
    public void TryParseSliceStatus_missing_in_use_defaults_to_not_in_use()
    {
        const string body = "slice 9 RF_frequency=14.200000 mode=USB";

        Assert.True(FlexSmartSdrCodec.TryParseSliceStatus(body, out var slice));
        Assert.Equal(9, slice.Index);
        Assert.Equal(14_200_000, slice.FrequencyHz);
        Assert.False(slice.InUse);
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
    public void BuildDisplayPanafallCreateCommand_formats()
    {
        var cmd = FlexSmartSdrCodec.BuildDisplayPanafallCreateCommand(5);
        Assert.Equal("C5|display panafall create\n", cmd);
    }

    [Fact]
    public void BuildDisplayPanRemoveCommands_format_pair()
    {
        var pan = FlexSmartSdrCodec.BuildDisplayPanRemoveCommand(6, "0x40000002");
        var panafall = FlexSmartSdrCodec.BuildDisplayPanafallRemoveCommand(7, "0x40000002");
        Assert.Equal("C6|display pan remove 0x40000002\n", pan);
        Assert.Equal("C7|display panafall remove 0x40000002\n", panafall);
    }

    [Theory]
    [InlineData("pan=0x40000012", "0x40000012")]
    [InlineData("id=0x4000000A", "0x4000000A")]
    [InlineData("0x4000000B", "0x4000000B")]
    public void TryParsePanafallCreatePanId_extracts_id(string body, string expected)
    {
        Assert.True(FlexSmartSdrCodec.TryParsePanafallCreatePanId(body, out var panId));
        Assert.Equal(expected, panId);
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

    [Fact]
    public void CommandBufferOptimization_SliceTuneCommand_MatchesExpectedFormat()
    {
        // This test verifies the StringBuilder command buffer optimization produces
        // functionally equivalent commands to the original string interpolation approach
        var cmd = FlexSmartSdrCodec.BuildSliceTuneCommand(123, 5, 435.150, autoPan: true);
        
        Assert.Equal("C123|slice tune 5 435.15 autopan=1\n", cmd);
    }

    [Fact] 
    public void CommandBufferOptimization_SliceSetModeCommand_MatchesExpectedFormat()
    {
        // Verifies StringBuilder optimization maintains exact functional equivalence for mode commands
        var cmd = FlexSmartSdrCodec.BuildSliceSetModeCommand(456, 2, "USB");
        
        Assert.Equal("C456|slice set 2 mode=USB\n", cmd);
    }

    [Fact]
    public void CommandBufferOptimization_DisplayPanCenterCommand_MatchesExpectedFormat()
    {
        // Verifies StringBuilder optimization handles frequency formatting correctly
        var cmd = FlexSmartSdrCodec.BuildDisplayPanCenterCommand(789, "0x40000003", 145.865);
        
        Assert.Equal("C789|display pan set 0x40000003 center=145.865 autocenter=0\n", cmd);
    }

    [Fact]
    public void CommandBufferOptimization_SliceCreateCommand_HandlesAllOptionalParameters()
    {
        // Verifies StringBuilder optimization correctly handles complex command with all optional parameters
        var cmd = FlexSmartSdrCodec.BuildSliceCreateCommand(999, 435.15, "FM", "RX_A", "0x40000005");
        
        Assert.Equal("C999|slice create freq=435.15 pan=0x40000005 mode=FM ant=RX_A\n", cmd);
    }

    [Fact]
    public void CommandBufferOptimization_SliceSetActiveCommand_HandlesBooleanFormatting()
    {
        // Verifies StringBuilder optimization correctly formats boolean values
        var activeCmd = FlexSmartSdrCodec.BuildSliceSetActiveCommand(100, 1, active: true);
        var inactiveCmd = FlexSmartSdrCodec.BuildSliceSetActiveCommand(101, 1, active: false);
        
        Assert.Equal("C100|slice set 1 active=1\n", activeCmd);
        Assert.Equal("C101|slice set 1 active=0\n", inactiveCmd);
    }

    [Fact]
    public void CommandBufferOptimization_FullDuplexCommand_HandlesBooleanValues()
    {
        // Verifies StringBuilder optimization for radio commands with boolean parameters
        var enabledCmd = FlexSmartSdrCodec.BuildFullDuplexCommand(200, enabled: true);
        var disabledCmd = FlexSmartSdrCodec.BuildFullDuplexCommand(201, enabled: false);
        
        Assert.Equal("C200|radio set full_duplex_enabled=1\n", enabledCmd);
        Assert.Equal("C201|radio set full_duplex_enabled=0\n", disabledCmd);
    }

    [Fact]
    public void CommandBufferOptimization_ToneCommands_HandleSpecialCharactersAndFloats()
    {
        // Verifies StringBuilder optimization handles tone mode text and floating point formatting
        var toneModeCmd = FlexSmartSdrCodec.BuildSliceSetToneModeCommand(300, 0, toneOn: true);
        var toneValueCmd = FlexSmartSdrCodec.BuildSliceSetToneValueCommand(301, 0, 67.0);
        var toneModeOffCmd = FlexSmartSdrCodec.BuildSliceSetToneModeCommand(302, 0, toneOn: false);
        
        Assert.Equal("C300|slice s 0 fm_tone_mode=ctcss_tx\n", toneModeCmd);
        Assert.Equal("C301|slice s 0 fm_tone_value=67.0\n", toneValueCmd);
        Assert.Equal("C302|slice s 0 fm_tone_mode=off\n", toneModeOffCmd);
    }

    [Fact]
    public void CommandBufferOptimization_AntennaCommands_HandleTokenSanitization()
    {
        // Verifies StringBuilder optimization correctly calls SanitizeToken for antenna ports
        var rxAntCmd = FlexSmartSdrCodec.BuildSliceSetRxAntCommand(400, 2, "RX_B");
        var txAntCmd = FlexSmartSdrCodec.BuildSliceSetTxAntCommand(401, 3, "XVTR");
        
        Assert.Equal("C400|slice set 2 rxant=RX_B\n", rxAntCmd);
        Assert.Equal("C401|slice set 3 txant=XVTR\n", txAntCmd);
    }

    [Fact]
    public void CommandBufferOptimization_ConcurrentCommandBuilding_DoesNotInterfere()
    {
        // Test that the lock-protected StringBuilder buffer handles concurrent command construction
        // This verifies thread safety of the optimization during high-frequency Doppler tracking
        const int CommandCount = 100;
        var tasks = new Task<string>[CommandCount];
        
        // Create many concurrent command building tasks with different parameters
        for (var i = 0; i < CommandCount; i++)
        {
            var sequence = (uint)(1000 + i);
            var sliceIndex = i % 4;  // Use different slice indices
            var frequency = 435.0 + (i * 0.01); // Use different frequencies
            var autoPan = i % 2 == 0; // Alternate autoPan values
            
            tasks[i] = Task.Run(() => 
                FlexSmartSdrCodec.BuildSliceTuneCommand(sequence, sliceIndex, frequency, autoPan));
        }
        
        // Wait for all tasks to complete
        Task.WaitAll(tasks);
        
        // Verify all commands were built correctly with no interference
        for (var i = 0; i < CommandCount; i++)
        {
            var expectedSequence = 1000 + i;
            var expectedSliceIndex = i % 4;
            var expectedFrequency = 435.0 + (i * 0.01);
            var expectedAutoPan = i % 2 == 0 ? "1" : "0";
            
            var result = tasks[i].Result;
            Assert.StartsWith($"C{expectedSequence}|slice tune {expectedSliceIndex} {expectedFrequency:0.######} autopan={expectedAutoPan}\n", result);
        }
    }
}
