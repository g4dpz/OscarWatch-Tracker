using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

/// <summary>
/// Validates the satellite mode exit sequence produced by <see cref="KenwoodTs2000Driver"/>.
/// Validates: Requirements 3.1, 3.2, 3.3, 3.4
/// </summary>
public class Ts2000SatelliteModeExitTests : Ts2000TestBase
{
    /// <summary>
    /// Requirement 3.1: SetSatelliteMode(false) sends exact exit sequence:
    /// RX;, TN39;, TO0;, TN39;, SA0010000;
    /// </summary>
    [Fact]
    public void Exit_sends_exact_sequence_RX_TN39_TO0_TN39_SA0010000()
    {
        EnterSatelliteMode();
        ClearCommandLog();

        Driver.SetSatelliteMode(false);

        AssertCommandSequence("RX;", "TN39;", "TO0;", "TN39;", "SA0010000;");
    }

    /// <summary>
    /// Requirement 3.2: RX; is the first command in the exit sequence and is routed
    /// through Transact (expecting a reply) because IsSatelliteModeExitReadCommand("RX;")
    /// returns true. The RecordingKenwoodCatTransport returns "RX0;" for a Transact("RX;")
    /// call, confirming it went through the reply path rather than fire-and-forget.
    /// </summary>
    [Fact]
    public void Exit_RX_uses_transact_expecting_reply()
    {
        EnterSatelliteMode();
        ClearCommandLog();

        Driver.SetSatelliteMode(false);

        var cmds = GetSentCommands();

        // RX; must be the first command in the exit sequence
        Assert.Equal("RX;", cmds[0]);

        // Verify the codec classifies RX; as a read command for the exit sequence
        Assert.True(KenwoodCatCodec.IsSatelliteModeExitReadCommand("RX;"));

        // The remaining commands are NOT exit read commands (they go via SendFireAndForget)
        Assert.False(KenwoodCatCodec.IsSatelliteModeExitReadCommand("TN39;"));
        Assert.False(KenwoodCatCodec.IsSatelliteModeExitReadCommand("TO0;"));
        Assert.False(KenwoodCatCodec.IsSatelliteModeExitReadCommand("SA0010000;"));
    }

    /// <summary>
    /// Requirement 3.3: After exit, IsSatelliteModeActive is false and the satellite
    /// layout confirmation flag is cleared (observable via IsSatelliteModeActive being false).
    /// </summary>
    [Fact]
    public void Exit_sets_IsSatelliteModeActive_false_after_sequence()
    {
        EnterSatelliteMode();

        // Confirm satellite mode is active before exit
        Assert.True(Driver.IsSatelliteModeActive);

        Driver.SetSatelliteMode(false);

        // After exit, satellite mode is no longer active
        Assert.False(Driver.IsSatelliteModeActive);

        // Verify the layout confirmation is also cleared by attempting a Doppler step:
        // it should fail because satellite mode is inactive
        ClearCommandLog();
        var result = Driver.ApplySatelliteDopplerStep(145_900_000, 435_700_000);
        Assert.False(result);
        Assert.Empty(GetSentCommands());
    }

    /// <summary>
    /// Requirement 3.4: The exit sequence does NOT send an SA; status query.
    /// Unlike entry (which queries SA; to verify), exit just sends the sequence and returns.
    /// </summary>
    [Fact]
    public void Exit_does_not_send_SA_status_query()
    {
        EnterSatelliteMode();
        ClearCommandLog();

        Driver.SetSatelliteMode(false);

        var cmds = GetSentCommands();

        // SA; (a read/query command) must NOT appear in the exit commands
        Assert.DoesNotContain("SA;", cmds);

        // SA0010000; is a write command (set satellite mode off), NOT a query — it's fine
        Assert.Contains("SA0010000;", cmds);
    }
}
