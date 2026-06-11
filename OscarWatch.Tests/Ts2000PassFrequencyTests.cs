using OscarWatch.Core.Radio;

namespace OscarWatch.Tests;

/// <summary>
/// Validates the pass frequency programming sequence sent when a satellite pass begins.
/// ApplySatellitePassFrequencies programs frequencies, modes, power, and link-hold polls.
/// Validates: Requirements 10.1, 10.2, 10.3, 10.4, 10.5, 10.6
/// </summary>
public class Ts2000PassFrequencyTests : Ts2000TestBase
{
    private const long DownlinkHz = 145_900_000;
    private const long UplinkHz = 435_700_000;
    private const char DownlinkModeCode = '2'; // USB
    private const char UplinkModeCode = '4';   // FM

    /// <summary>
    /// Requirement 10.1: ApplySatellitePassFrequencies sends double FA/FB commands
    /// followed by SM band-select commands (main and sub).
    /// </summary>
    [Fact]
    public void PassFrequencies_sends_double_FA_FB_followed_by_SM_commands()
    {
        EnterSatelliteMode();
        ClearCommandLog();

        Driver.ApplySatellitePassFrequencies(DownlinkHz, UplinkHz, DownlinkModeCode, UplinkModeCode);

        var cmds = GetSentCommands();
        var expectedFa = $"FA{DownlinkHz:D11};";
        var expectedFb = $"FB{UplinkHz:D11};";
        var expectedSmMain = KenwoodCatCodec.BuildSatelliteBandSelectMainCommand(); // SM10000;
        var expectedSmSub = KenwoodCatCodec.BuildSatelliteBandSelectSubCommand(DownlinkHz); // SM00021; for < 200 MHz

        // ProgramSatelliteFrequencies sends: FA, FB, FA, FB, SM-main, SM-sub, SA1010110, TO0
        Assert.Equal(expectedFa, cmds[0]);
        Assert.Equal(expectedFb, cmds[1]);
        Assert.Equal(expectedFa, cmds[2]);
        Assert.Equal(expectedFb, cmds[3]);
        Assert.Equal(expectedSmMain, cmds[4]);
        Assert.Equal(expectedSmSub, cmds[5]);
    }

    /// <summary>
    /// Requirement 10.2: The downlink mode is set using MD{downlinkModeCode}; on the main
    /// path with SA main-control (SA1010110;) active.
    /// </summary>
    [Fact]
    public void PassFrequencies_sets_downlink_mode_on_main_path()
    {
        EnterSatelliteMode();
        ClearCommandLog();

        Driver.ApplySatellitePassFrequencies(DownlinkHz, UplinkHz, DownlinkModeCode, UplinkModeCode);

        var cmds = GetSentCommands();
        var expectedSaMain = "SA1010110;";
        var expectedMd = $"MD{DownlinkModeCode};";

        // FinalizeSatelliteMainPath sends SA1010110; ... SA1010110; MD2; ...
        // Find the MD command in the main path section and verify SA main-control precedes it
        var mdIndex = cmds.ToList().IndexOf(expectedMd);
        Assert.True(mdIndex > 0, $"Expected MD{DownlinkModeCode}; in command sequence");

        // The SA1010110; immediately before the MD command ensures main-control is active
        Assert.Equal(expectedSaMain, cmds[mdIndex - 1]);
    }

    /// <summary>
    /// Requirement 10.3: The uplink mode is set using MD{uplinkModeCode}; on the sub path
    /// with SA sub-control (SA1011110;) active, bracketed by SA commands.
    /// </summary>
    [Fact]
    public void PassFrequencies_sets_uplink_mode_on_sub_path_with_SA_bracketing()
    {
        EnterSatelliteMode();
        ClearCommandLog();

        Driver.ApplySatellitePassFrequencies(DownlinkHz, UplinkHz, DownlinkModeCode, UplinkModeCode);

        var cmds = GetSentCommands();
        var expectedSaSub = "SA1011110;";
        var expectedMdUplink = $"MD{UplinkModeCode};";

        // FinalizeSatelliteSubPath sends: SA1011110; MD4; ...
        var saSubIndex = cmds.ToList().IndexOf(expectedSaSub);
        Assert.True(saSubIndex >= 0, "Expected SA1011110; (sub-control) in command sequence");
        Assert.Equal(expectedMdUplink, cmds[saSubIndex + 1]);
    }

    /// <summary>
    /// Requirement 10.4: ApplySatellitePassFrequencies sends PC050; to set the RF power level.
    /// </summary>
    [Fact]
    public void PassFrequencies_sends_PC050_power_level()
    {
        EnterSatelliteMode();
        ClearCommandLog();

        Driver.ApplySatellitePassFrequencies(DownlinkHz, UplinkHz, DownlinkModeCode, UplinkModeCode);

        AssertCommandContains("PC050;");
    }

    /// <summary>
    /// Requirement 10.5: The programming sequence ends with SA1010110; then AI0; followed
    /// by exactly 7 FA; link-hold polls.
    /// </summary>
    [Fact]
    public void PassFrequencies_ends_with_SA1010110_AI0_and_7_link_hold_polls()
    {
        EnterSatelliteMode();
        ClearCommandLog();

        Driver.ApplySatellitePassFrequencies(DownlinkHz, UplinkHz, DownlinkModeCode, UplinkModeCode);

        var cmds = GetSentCommands();

        // The last 9 commands should be: SA1010110;, AI0;, then 7x FA;
        var totalCount = cmds.Count;
        Assert.True(totalCount >= 9, $"Expected at least 9 commands at end of sequence, got {totalCount} total");

        // 7 FA; polls at the end
        for (var i = totalCount - 7; i < totalCount; i++)
        {
            Assert.Equal("FA;", cmds[i]);
        }

        // AI0; before the polls
        Assert.Equal("AI0;", cmds[totalCount - 8]);

        // SA1010110; before AI0;
        Assert.Equal("SA1010110;", cmds[totalCount - 9]);
    }

    /// <summary>
    /// Requirement 10.6: When not in satellite mode, ApplySatellitePassFrequencies sends no commands.
    /// </summary>
    [Fact]
    public void PassFrequencies_no_commands_when_not_in_satellite_mode()
    {
        // Do NOT call EnterSatelliteMode() — driver is in normal VFO mode
        ClearCommandLog();

        Driver.ApplySatellitePassFrequencies(DownlinkHz, UplinkHz, DownlinkModeCode, UplinkModeCode);

        Assert.Empty(GetSentCommands());
    }
}
