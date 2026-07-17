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
    /// SM rejections must not abort later FA/FB in the cluster.
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
    /// Requirement 5.2: Doppler step sends only the 8-command frequency cluster (no link-hold burst).
    /// Link-hold FA; polls run on a ~1/s timer from RigController.
    /// </summary>
    [Fact]
    public void DopplerStep_sends_only_frequency_cluster_without_link_hold_burst()
    {
        long downlinkHz = 145_900_000;
        long uplinkHz = 435_700_000;

        EnterSatelliteMode();
        ClearCommandLog();

        Driver.ApplySatelliteDopplerStep(downlinkHz, uplinkHz);

        var cmds = GetSentCommands();

        Assert.Equal(8, cmds.Count);
        Assert.DoesNotContain(cmds, c => c == "FA;");
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

    /// <summary>
    /// Field report: SM10000 rejected every tick. FA/FB must still complete the full cluster.
    /// </summary>
    [Fact]
    public void DopplerStep_completes_FA_FB_cluster_when_SM_is_rejected()
    {
        const long downlinkHz = 145_900_000;
        const long uplinkHz = 435_700_000;

        EnterSatelliteMode();
        ClearCommandLog();
        RecordingTransport.RejectedPrefixes.Add("SM");

        Assert.True(Driver.ApplySatelliteDopplerStep(downlinkHz, uplinkHz));

        var cmds = GetSentCommands();
        var expectedFa = $"FA{downlinkHz:D11};";
        var expectedFb = $"FB{uplinkHz:D11};";

        Assert.Equal(8, cmds.Count);
        Assert.Equal(expectedFa, cmds[0]);
        Assert.Equal(expectedFb, cmds[1]);
        Assert.Equal(expectedFa, cmds[3]);
        Assert.Equal(expectedFb, cmds[5]);
    }
}
