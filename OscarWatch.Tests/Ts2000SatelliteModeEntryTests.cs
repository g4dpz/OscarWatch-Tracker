using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

/// <summary>
/// Validates the full satellite mode entry handshake sequence produced by <see cref="KenwoodTs2000Driver"/>.
/// Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 2.1, 2.2, 2.3, 2.4, 2.5
/// </summary>
public class Ts2000SatelliteModeEntryTests : Ts2000TestBase
{
    /// <summary>
    /// Requirement 1.1: SA1010110; is sent first, followed by settling delay, then TO0;TO0;
    /// </summary>
    [Fact]
    public void Entry_sends_SA1010110_first_then_settling_then_tone_off()
    {
        EnterSatelliteMode();

        var cmds = GetSentCommands();

        // First command must be SA1010110; (enter satellite mode, fire and forget)
        Assert.Equal("SA1010110;", cmds[0]);

        // After settling delay (0ms in tests), tone-off pair follows
        Assert.Equal("TO0;", cmds[1]);
        Assert.Equal("TO0;", cmds[2]);
    }

    /// <summary>
    /// Requirement 1.3: Entry handshake ordering is FA;, TS1;, AI2;, SA1010110;
    /// </summary>
    [Fact]
    public void Entry_handshake_sends_FA_TS1_AI2_SA1010110_in_order()
    {
        EnterSatelliteMode();

        var cmds = GetSentCommands();

        // After the initial SA1010110; + TO0; TO0;, the handshake begins at index 3
        Assert.Equal("FA;", cmds[3]);
        Assert.Equal("TS1;", cmds[4]);
        Assert.Equal("AI2;", cmds[5]);
        Assert.Equal("SA1010110;", cmds[6]);
    }

    /// <summary>
    /// Requirement 1.4: Mode set on main (MD2;) and sub (MD1;) with SA1011110; bracketing the sub command.
    /// The sequence after the first four handshake commands is:
    /// SA1010110;, MD2;, SA1011110;, MD1;, SA1010110;, TO0;
    /// </summary>
    [Fact]
    public void Entry_handshake_sets_modes_with_SA_sub_control_bracketing()
    {
        EnterSatelliteMode();

        var cmds = GetSentCommands();

        // Second SA1010110; (index 7) precedes MD2; on main
        Assert.Equal("SA1010110;", cmds[7]);
        Assert.Equal("MD2;", cmds[8]);

        // SA1011110; switches to sub-control before MD1;
        Assert.Equal("SA1011110;", cmds[9]);
        Assert.Equal("MD1;", cmds[10]);

        // SA1010110; returns to main-control after sub mode set
        Assert.Equal("SA1010110;", cmds[11]);

        // TO0; finishes the handshake
        Assert.Equal("TO0;", cmds[12]);
    }

    /// <summary>
    /// Requirement 1.5: SA; verification query is sent after the entry handshake.
    /// </summary>
    [Fact]
    public void Entry_sends_SA_verification_query_after_handshake()
    {
        EnterSatelliteMode();

        var cmds = GetSentCommands();

        // SA; is the verification transact after the handshake (index 13)
        Assert.Equal("SA;", cmds[13]);
        AssertCommandContains("SA;");
    }

    /// <summary>
    /// Requirement 1.6: After SA; confirms satellite mode, tone/CTCSS is cleared on both paths.
    /// The SendSatelliteToneAndSquelchOff sequence is:
    ///   Main path: SA1010110;, TO0;, DQ0;, CT0;
    ///   Sub path:  SA1011110;, TO0;, DQ0;, CT0;
    ///   Final:     SA1010110;
    /// </summary>
    [Fact]
    public void Entry_clears_tone_and_ctcss_on_both_paths_after_SA_confirms()
    {
        EnterSatelliteMode();

        var cmds = GetSentCommands();

        // After SA; at index 13, the tone/CTCSS clearing begins at index 14
        // Main path clear: SA1010110;, TO0;, DQ0;, CT0;
        Assert.Equal("SA1010110;", cmds[14]);
        Assert.Equal("TO0;", cmds[15]);
        Assert.Equal("DQ0;", cmds[16]);
        Assert.Equal("CT0;", cmds[17]);

        // Sub path clear: SA1011110;, TO0;, DQ0;, CT0;
        Assert.Equal("SA1011110;", cmds[18]);
        Assert.Equal("TO0;", cmds[19]);
        Assert.Equal("DQ0;", cmds[20]);
        Assert.Equal("CT0;", cmds[21]);

        // Final return to main-control
        Assert.Equal("SA1010110;", cmds[22]);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // SA Verification Retry Tests — Requirements 2.1, 2.2, 2.3, 2.4, 2.5
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Requirement 2.1: SA; is retried up to the configurable max attempts (3) when the radio
    /// does not confirm satellite mode. Verifies 3 SA; commands appear in the command log.
    /// </summary>
    [Fact]
    public void SA_verification_retries_up_to_max_attempts_when_not_confirming()
    {
        // Use a transport that never confirms satellite mode (doesn't auto-flip on SA1010110;)
        var transport = new NonConfirmingSatelliteTransport();
        var driver = new KenwoodTs2000Driver(transport, catDelayMs: 0, satModeSettlingDelayMs: 0, satModeRetryCount: 3, satModeRetryDelayMs: 0);
        driver.Open();

        driver.SetSatelliteMode(true);

        // SA; should appear exactly 3 times (the retry count)
        var saCount = transport.SentCommands.Count(c => c == "SA;");
        Assert.Equal(3, saCount);
    }

    /// <summary>
    /// Requirement 2.2: IsSatelliteModeActive = true when SA; confirms on a retry attempt.
    /// Uses a transport that fails on the first SA; query but confirms on the second.
    /// </summary>
    [Fact]
    public void IsSatelliteModeActive_true_when_SA_confirms_on_second_attempt()
    {
        var transport = new DelayedConfirmingSatelliteTransport(confirmOnAttempt: 2);
        var driver = new KenwoodTs2000Driver(transport, catDelayMs: 0, satModeSettlingDelayMs: 0, satModeRetryCount: 3, satModeRetryDelayMs: 0);
        driver.Open();

        driver.SetSatelliteMode(true);

        Assert.True(driver.IsSatelliteModeActive);
    }

    /// <summary>
    /// Requirement 2.3: IsSatelliteModeActive = false when all SA; retries exhaust
    /// without confirmation (radio never confirms SATL).
    /// </summary>
    [Fact]
    public void IsSatelliteModeActive_false_when_all_retries_exhaust()
    {
        var transport = new NonConfirmingSatelliteTransport();
        var driver = new KenwoodTs2000Driver(transport, catDelayMs: 0, satModeSettlingDelayMs: 0, satModeRetryCount: 3, satModeRetryDelayMs: 0);
        driver.Open();

        driver.SetSatelliteMode(true);

        Assert.False(driver.IsSatelliteModeActive);
    }

    /// <summary>
    /// Requirement 2.4: No further satellite tracking commands sent after SA; retries exhaust.
    /// After SetSatelliteMode(true) fails, ApplySatelliteDopplerStep should return false
    /// and send no commands.
    /// </summary>
    [Fact]
    public void No_tracking_commands_sent_after_SA_retries_exhaust()
    {
        var transport = new NonConfirmingSatelliteTransport();
        var driver = new KenwoodTs2000Driver(transport, catDelayMs: 0, satModeSettlingDelayMs: 0, satModeRetryCount: 3, satModeRetryDelayMs: 0);
        driver.Open();

        driver.SetSatelliteMode(true);

        // Clear the command log after entry attempt
        transport.SentCommands.Clear();

        // Attempting a Doppler step should return false and send no commands
        var result = driver.ApplySatelliteDopplerStep(145_900_000, 435_700_000);

        Assert.False(result);
        Assert.Empty(transport.SentCommands);
    }

    /// <summary>
    /// Requirement 2.5: FA/FB frequency commands used without FR or DC prefix
    /// when IsSatelliteModeActive is true after a confirmed entry.
    /// After entering satellite mode and doing a Doppler step, verify no "FR" or "DC"
    /// commands appear in the log.
    /// </summary>
    [Fact]
    public void FA_FB_used_without_FR_or_DC_prefix_when_satellite_mode_active()
    {
        // Use default base class transport which confirms satellite mode
        EnterSatelliteMode();
        ClearCommandLog();

        // Perform a Doppler step
        Driver.ApplySatelliteDopplerStep(145_900_000, 435_700_000);

        var cmds = GetSentCommands();

        // FA and FB commands must be present
        Assert.Contains(cmds, c => c.StartsWith("FA", StringComparison.Ordinal) && c != "FA;");
        Assert.Contains(cmds, c => c.StartsWith("FB", StringComparison.Ordinal) && c != "FB;");

        // No FR or DC commands should appear
        AssertNoCommandStartingWith("FR");
        AssertNoCommandStartingWith("DC");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Helper transports for SA retry tests
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Transport that simulates a radio that NEVER confirms satellite mode.
    /// Unlike RecordingKenwoodCatTransport, this does NOT auto-flip SatelliteStatusOn
    /// when SA1010110; is sent — SA; always returns "SA0;".
    /// </summary>
    private sealed class NonConfirmingSatelliteTransport : IKenwoodCatTransport
    {
        public List<string> SentCommands { get; } = [];
        public bool IsOpen { get; private set; }

        public void Open() => IsOpen = true;

        public bool SendFireAndForget(string command, int postDelayMs = 50)
        {
            SentCommands.Add(Normalize(command));
            return true;
        }

        public bool SendCommand(string command, int postDelayMs = 50)
        {
            SentCommands.Add(Normalize(command));
            return true;
        }

        public string? Transact(string command, int postDelayMs = 50)
        {
            var normalized = Normalize(command);
            SentCommands.Add(normalized);

            return normalized switch
            {
                "SA;" => "SA0;",
                "FA;" => "FA00435750000;",
                _ => null
            };
        }

        public void Dispose() => IsOpen = false;

        private static string Normalize(string command)
        {
            var cmd = command.Trim();
            return cmd.EndsWith(';') ? cmd : cmd + ";";
        }
    }

    /// <summary>
    /// Transport that simulates a radio confirming satellite mode only on a specific SA; query attempt.
    /// Before that attempt, SA; returns "SA0;". On and after that attempt, returns "SA1;".
    /// </summary>
    private sealed class DelayedConfirmingSatelliteTransport : IKenwoodCatTransport
    {
        private readonly int _confirmOnAttempt;
        private int _saQueryCount;

        public DelayedConfirmingSatelliteTransport(int confirmOnAttempt)
        {
            _confirmOnAttempt = confirmOnAttempt;
        }

        public List<string> SentCommands { get; } = [];
        public bool IsOpen { get; private set; }

        public void Open() => IsOpen = true;

        public bool SendFireAndForget(string command, int postDelayMs = 50)
        {
            SentCommands.Add(Normalize(command));
            return true;
        }

        public bool SendCommand(string command, int postDelayMs = 50)
        {
            SentCommands.Add(Normalize(command));
            return true;
        }

        public string? Transact(string command, int postDelayMs = 50)
        {
            var normalized = Normalize(command);
            SentCommands.Add(normalized);

            return normalized switch
            {
                "SA;" => HandleSaQuery(),
                "FA;" => "FA00435750000;",
                _ => null
            };
        }

        public void Dispose() => IsOpen = false;

        private string HandleSaQuery()
        {
            _saQueryCount++;
            return _saQueryCount >= _confirmOnAttempt ? "SA1;" : "SA0;";
        }

        private static string Normalize(string command)
        {
            var cmd = command.Trim();
            return cmd.EndsWith(';') ? cmd : cmd + ";";
        }
    }
}
