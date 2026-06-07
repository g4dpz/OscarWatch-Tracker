using OscarWatch.Core.Radio;

namespace OscarWatch.Tests;

/// <summary>
/// Validates the complete Doppler step command cluster sent during satellite tracking:
/// FA/FB frequency commands, SM band-select, and link-hold polling sequence.
/// Validates: Requirements 5.1, 5.2, 5.3, 5.4
/// </summary>
public class Ts2000DopplerStepTests : Ts2000TestBase
{
    /// <summary>
    /// Requirement 5.1: ApplySatelliteDopplerStep sends the full cluster sequence:
    /// FA, FB, SM10000, FA, SM-sub, FB, SM-sub, SM10000 in that order.
    /// </summary>
    [Fact]
    public void DopplerStep_sends_full_cluster_sequence()
    {
        long downlinkHz = 145_900_000;
        long uplinkHz = 435_700_000;

        EnterSatelliteMode();
        ClearCommandLog();

        Driver.ApplySatelliteDopplerStep(downlinkHz, uplinkHz);

        var cmds = GetSentCommands();

        // The cluster is the first 8 commands
        var expectedFa = $"FA{downlinkHz:D11};";
        var expectedFb = $"FB{uplinkHz:D11};";
        var expectedSmMain = "SM10000;";
        var expectedSmSub = KenwoodCatCodec.BuildSatelliteBandSelectSubCommand(downlinkHz); // SM00021; for < 200 MHz

        Assert.True(cmds.Count >= 8, $"Expected at least 8 cluster commands, got {cmds.Count}");
        Assert.Equal(expectedFa, cmds[0]);
        Assert.Equal(expectedFb, cmds[1]);
        Assert.Equal(expectedSmMain, cmds[2]);
        Assert.Equal(expectedFa, cmds[3]);
        Assert.Equal(expectedSmSub, cmds[4]);
        Assert.Equal(expectedFb, cmds[5]);
        Assert.Equal(expectedSmSub, cmds[6]);
        Assert.Equal(expectedSmMain, cmds[7]);
    }

    /// <summary>
    /// Requirement 5.2: After the 8-command frequency cluster, exactly 7 FA; link-hold
    /// polls are sent via Transact. Total = 8 cluster + 7 polls = 15 commands.
    /// </summary>
    [Fact]
    public void DopplerStep_sends_exactly_7_FA_link_hold_polls_after_cluster()
    {
        long downlinkHz = 145_900_000;
        long uplinkHz = 435_700_000;

        EnterSatelliteMode();
        ClearCommandLog();

        Driver.ApplySatelliteDopplerStep(downlinkHz, uplinkHz);

        var cmds = GetSentCommands();

        // Total should be 15: 8 cluster + 7 polls
        Assert.Equal(15, cmds.Count);

        // Commands at indices 8..14 should all be "FA;" (link-hold polls)
        for (var i = 8; i < 15; i++)
        {
            Assert.Equal("FA;", cmds[i]);
        }
    }

    /// <summary>
    /// Requirement 5.3: ApplySatelliteDopplerStep returns false and sends no commands
    /// when the driver is not in satellite mode.
    /// </summary>
    [Fact]
    public void DopplerStep_returns_false_and_no_commands_when_not_in_satellite_mode()
    {
        // Do NOT call EnterSatelliteMode() — driver is in normal VFO mode
        ClearCommandLog();

        var result = Driver.ApplySatelliteDopplerStep(145_900_000, 435_700_000);

        Assert.False(result);
        Assert.Empty(GetSentCommands());
    }

    /// <summary>
    /// Requirement 5.4: ApplySatelliteDopplerStep returns false and sends no commands
    /// when called with a zero frequency.
    /// </summary>
    [Fact]
    public void DopplerStep_returns_false_and_no_commands_with_zero_frequency()
    {
        EnterSatelliteMode();
        ClearCommandLog();

        var result = Driver.ApplySatelliteDopplerStep(0, 435_700_000);

        Assert.False(result);
        Assert.Empty(GetSentCommands());
    }

    /// <summary>
    /// Requirement 5.4: ApplySatelliteDopplerStep returns false and sends no commands
    /// when called with a negative frequency.
    /// </summary>
    [Fact]
    public void DopplerStep_returns_false_and_no_commands_with_negative_frequency()
    {
        EnterSatelliteMode();
        ClearCommandLog();

        var result = Driver.ApplySatelliteDopplerStep(-145_900_000, 435_700_000);

        Assert.False(result);
        Assert.Empty(GetSentCommands());
    }
}
