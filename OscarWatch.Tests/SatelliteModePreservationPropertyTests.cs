// Feature: ts2000-satmode-entry-fix, Property 2: Preservation — Non-Entry-Path Behavior Unchanged

using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6**
///
/// Preservation property tests: verify that all non-entry-path behaviors remain unchanged.
/// These tests MUST PASS on the unfixed code (they capture the baseline to protect).
/// After the fix, these tests must still pass (confirming no regressions).
/// </summary>
public class SatelliteModePreservationPropertyTests
{
    // ────────────────────────────────────────────────────────────────────────────
    // Property 2a: Exit sequence preservation
    // Validates: Requirement 3.2
    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any number of SetSatelliteMode(false) calls, the exit command sequence is always
    /// exactly: RX;, TN39;, TO0;, TN39;, SA0010000; — unchanged.
    /// </summary>
    [Property(MaxTest = 10)]
    public bool Exit_sequence_is_always_RX_TN39_TO0_TN39_SA0010000(byte callCountByte)
    {
        var callCount = (callCountByte % 5) + 1; // 1-5 exit calls

        var transport = new RecordingKenwoodCatTransport { SatelliteStatusOn = true };
        var driver = new KenwoodTs2000Driver(transport, catDelayMs: 0);
        driver.Open();

        for (var i = 0; i < callCount; i++)
        {
            // Enter satellite mode first (so exit has something to do)
            driver.SetSatelliteMode(true);
            transport.SentCommands.Clear();

            // Exit satellite mode
            driver.SetSatelliteMode(false);

            // Assert the exact exit sequence
            string[] expectedExit = ["RX;", "TN39;", "TO0;", "TN39;", "SA0010000;"];
            if (transport.SentCommands.Count != expectedExit.Length)
                return false;

            for (var j = 0; j < expectedExit.Length; j++)
            {
                if (transport.SentCommands[j] != expectedExit[j])
                    return false;
            }
        }

        return true;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Property 2b: Offline pre-configuration preservation
    // Validates: Requirement 3.5
    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any SetSatelliteMode(true) call with a closed transport, _satelliteMode is set
    /// to true without sending any serial commands.
    /// </summary>
    [Property(MaxTest = 10)]
    public bool Offline_preconfig_sets_satellite_mode_without_serial_IO(bool initialState)
    {
        var transport = new RecordingKenwoodCatTransport();
        // Do NOT open the transport — simulates offline pre-configuration
        var driver = new KenwoodTs2000Driver(transport, catDelayMs: 0);

        // Set initial state
        if (initialState)
        {
            // Can't use SetSatelliteMode(true) on open transport here,
            // so just set it via closed transport (which is exactly what we're testing)
        }

        transport.SentCommands.Clear();
        driver.SetSatelliteMode(true);

        // Assert: _satelliteMode is true
        if (!driver.IsSatelliteModeActive)
            return false;

        // Assert: no commands were sent (transport was not open)
        if (transport.SentCommands.Count != 0)
            return false;

        return true;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Property 2c: First-query success path preservation
    // Validates: Requirement 3.1
    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When SA; returns SA1; on the first query (radio confirms SATL immediately),
    /// the driver sets _satelliteMode = true and sends tone/squelch clearing commands.
    /// This is the happy path that must remain unchanged.
    /// </summary>
    [Property(MaxTest = 10)]
    public bool First_query_success_sets_mode_true_and_sends_tone_squelch_clearing(byte unusedSeed)
    {
        // RecordingKenwoodCatTransport with SatelliteStatusOn = true auto-confirms SA;
        var transport = new RecordingKenwoodCatTransport { SatelliteStatusOn = true };
        var driver = new KenwoodTs2000Driver(transport, catDelayMs: 0);
        driver.Open();

        driver.SetSatelliteMode(true);

        // Assert: satellite mode is active
        if (!driver.IsSatelliteModeActive)
            return false;

        // Assert: SA; query was sent and only once (no retries needed)
        var saQueryCount = transport.SentCommands.Count(c => c == "SA;");
        if (saQueryCount != 1)
            return false;

        // Assert: tone/squelch clearing was sent (TO0; commands after SA; confirmation)
        // The SendSatelliteToneAndSquelchOff sends: SA1010110;, TO0;, DQ0;, CT0; (main path)
        // then SA1011110;, TO0;, DQ0;, CT0; (sub path), then SA1010110; (final)
        var saQueryIndex = transport.SentCommands.IndexOf("SA;");
        var commandsAfterSa = transport.SentCommands.Skip(saQueryIndex + 1).ToList();

        // Must contain TO0; (tone off) after SA; query
        if (!commandsAfterSa.Contains("TO0;"))
            return false;

        // Must contain CT0; (CTCSS off) after SA; query
        if (!commandsAfterSa.Contains("CT0;"))
            return false;

        // Must contain DQ0; after SA; query
        if (!commandsAfterSa.Contains("DQ0;"))
            return false;

        return true;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Property 2d: ApplySatelliteDopplerStep preservation
    // Validates: Requirements 3.3
    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any valid downlink/uplink frequency pair, ApplySatelliteDopplerStep produces
    /// the FA/FB/SM frequency cluster followed by 7 FA; link-hold polls.
    /// The exact sequence pattern is: FA, FB, SM10000, FA, SM(sub), FB, SM(sub), SM10000,
    /// then 7x FA; polls.
    /// </summary>
    [Property(MaxTest = 10)]
    public bool DopplerStep_produces_FA_FB_SM_cluster_and_link_hold_polls(
        int downlinkSeed, int uplinkSeed)
    {
        // Generate valid frequencies in ham radio satellite bands
        // Downlink: 144-148 MHz or 435-438 MHz
        // Uplink: 144-148 MHz or 435-438 MHz
        var downlinkHz = GenerateValidFrequency(downlinkSeed, isDownlink: true);
        var uplinkHz = GenerateValidFrequency(uplinkSeed, isDownlink: false);

        var transport = new RecordingKenwoodCatTransport { SatelliteStatusOn = true };
        var driver = new KenwoodTs2000Driver(transport, catDelayMs: 0);
        driver.Open();
        driver.SetSatelliteMode(true);
        transport.SentCommands.Clear();

        var result = driver.ApplySatelliteDopplerStep(downlinkHz, uplinkHz);

        if (!result)
            return false;

        var cmds = transport.SentCommands;

        // Expected FA command for downlink
        var expectedFa = $"FA{downlinkHz:D11};";
        var expectedFb = $"FB{uplinkHz:D11};";

        // The sequence should be: FA, FB, SM10000, FA, SM(sub), FB, SM(sub), SM10000, then 7x FA;
        // Total = 8 cluster commands + 7 poll commands = 15
        if (cmds.Count != 15)
            return false;

        // Verify cluster pattern
        if (cmds[0] != expectedFa) return false;
        if (cmds[1] != expectedFb) return false;
        if (cmds[2] != "SM10000;") return false;
        if (cmds[3] != expectedFa) return false;

        // SM sub depends on frequency band: >= 200MHz => SM00004; else SM00021;
        var expectedSmSub = downlinkHz >= 200_000_000 ? "SM00004;" : "SM00021;";
        if (cmds[4] != expectedSmSub) return false;
        if (cmds[5] != expectedFb) return false;
        if (cmds[6] != expectedSmSub) return false;
        if (cmds[7] != "SM10000;") return false;

        // Verify 7 FA; link-hold polls
        for (var i = 8; i < 15; i++)
        {
            if (cmds[i] != "FA;")
                return false;
        }

        return true;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Property 2e: ApplySatellitePassFrequencies preservation
    // Validates: Requirements 3.4
    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any valid frequency and mode-code combination, ApplySatellitePassFrequencies
    /// produces the double FA/FB, SM, mode, PC050, and tone sequence followed by
    /// 7 FA; link-hold polls.
    /// </summary>
    [Property(MaxTest = 10)]
    public bool PassFrequencies_produces_programming_sequence_and_link_hold_polls(
        int downlinkSeed, int uplinkSeed, byte modeIndexByte)
    {
        var downlinkHz = GenerateValidFrequency(downlinkSeed, isDownlink: true);
        var uplinkHz = GenerateValidFrequency(uplinkSeed, isDownlink: false);

        // Valid TS-2000 mode codes: '1' (LSB), '2' (USB), '3' (CW), '4' (FM), '5' (AM)
        char[] validModes = ['1', '2', '3', '4', '5'];
        var downlinkModeCode = validModes[modeIndexByte % validModes.Length];
        var uplinkModeCode = validModes[(modeIndexByte / 5) % validModes.Length];

        var transport = new RecordingKenwoodCatTransport { SatelliteStatusOn = true };
        var driver = new KenwoodTs2000Driver(transport, catDelayMs: 0);
        driver.Open();
        driver.SetSatelliteMode(true);
        transport.SentCommands.Clear();

        driver.ApplySatellitePassFrequencies(downlinkHz, uplinkHz, downlinkModeCode, uplinkModeCode);

        var cmds = transport.SentCommands;

        var expectedFa = $"FA{downlinkHz:D11};";
        var expectedFb = $"FB{uplinkHz:D11};";
        var expectedSmSub = downlinkHz >= 200_000_000 ? "SM00004;" : "SM00021;";

        // Verify key commands are present in the sequence:
        // 1. Double FA/FB (ProgramSatelliteFrequencies sends FA, FB, FA, FB)
        var faCount = cmds.Count(c => c == expectedFa);
        if (faCount < 2) return false;

        var fbCount = cmds.Count(c => c == expectedFb);
        if (fbCount < 2) return false;

        // 2. SM band select commands present
        if (!cmds.Contains("SM10000;")) return false;
        if (!cmds.Contains(expectedSmSub)) return false;

        // 3. Mode commands (MD) for downlink and uplink
        var expectedDownlinkMode = $"MD{downlinkModeCode};";
        var expectedUplinkMode = $"MD{uplinkModeCode};";
        if (!cmds.Contains(expectedDownlinkMode)) return false;
        if (!cmds.Contains(expectedUplinkMode)) return false;

        // 4. PC050; power level command
        if (!cmds.Contains("PC050;")) return false;

        // 5. SA1010110; and SA1011110; control commands (satellite mode on / sub control)
        if (!cmds.Contains("SA1010110;")) return false;
        if (!cmds.Contains("SA1011110;")) return false;

        // 6. TO0; tone off commands
        if (!cmds.Contains("TO0;")) return false;

        // 7. AI0; autoinfo off at the end
        if (!cmds.Contains("AI0;")) return false;

        // 8. 7 FA; link-hold polls at the end
        var linkHoldPolls = cmds.Count(c => c == "FA;");
        if (linkHoldPolls != 7) return false;

        // 9. The link-hold polls must be at the end
        var lastNonPollIndex = -1;
        for (var i = cmds.Count - 1; i >= 0; i--)
        {
            if (cmds[i] != "FA;")
            {
                lastNonPollIndex = i;
                break;
            }
        }

        // All commands after lastNonPollIndex should be FA;
        for (var i = lastNonPollIndex + 1; i < cmds.Count; i++)
        {
            if (cmds[i] != "FA;") return false;
        }

        // And there should be exactly 7 of them
        if (cmds.Count - lastNonPollIndex - 1 != 7) return false;

        return true;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a valid amateur satellite frequency from a seed integer.
    /// Constrains to realistic VHF/UHF ham bands used for satellite work.
    /// </summary>
    private static long GenerateValidFrequency(int seed, bool isDownlink)
    {
        // Use absolute value and modulo to constrain to valid ranges
        var absSeed = Math.Abs((long)seed);

        if (isDownlink)
        {
            // Downlink bands: 145.800-146.000 MHz (2m) or 435.000-438.000 MHz (70cm)
            if (absSeed % 2 == 0)
                return 145_800_000 + (absSeed % 200_000); // 145.800-146.000 MHz
            else
                return 435_000_000 + (absSeed % 3_000_000); // 435.000-438.000 MHz
        }
        else
        {
            // Uplink bands: 145.900-146.000 MHz (2m) or 435.000-438.000 MHz (70cm)
            if (absSeed % 2 == 0)
                return 145_900_000 + (absSeed % 100_000); // 145.900-146.000 MHz
            else
                return 435_000_000 + (absSeed % 3_000_000); // 435.000-438.000 MHz
        }
    }
}
