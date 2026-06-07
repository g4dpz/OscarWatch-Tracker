// Feature: ts2000-hardware-validation, Properties 3, 4, 7, 8

using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

/// <summary>
/// Property-based tests for guard conditions and error recovery paths in
/// the <see cref="KenwoodTs2000Driver"/> satellite mode lifecycle.
///
/// Property 3: Exit Sequence Idempotence
/// Property 4: SA Retry Exhaustion
/// Property 7: Guard Condition — Not In Sat Mode
/// Property 8: Guard Condition — Zero/Negative Frequency
/// </summary>
public class Ts2000GuardAndRecoveryPropertyTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Property 3: Exit Sequence Idempotence
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Validates: Requirements 3.1, 3.2, 3.3, 3.4**
    ///
    /// Property 3: Exit Sequence Idempotence
    /// ∀ n ∈ [1,5]: calling SetSatelliteMode(false) n times after entry always produces
    /// the same exit sequence per call: [RX;, TN39;, TO0;, TN39;, SA0010000;]
    ///
    /// After the first exit, the driver is no longer in satellite mode, so subsequent
    /// exits go through the "transport is open but not in sat mode" path which still
    /// produces the exit sequence (because _satelliteMode was already false, so the
    /// method sets false again and calls SendSatelliteModeExitSequence).
    ///
    /// Actually, looking at the driver: if on==false, it sets _satelliteMode=false and
    /// calls SendSatelliteModeExitSequence unconditionally. So each call produces the
    /// same 5-command sequence.
    /// </summary>
    [Property(MaxTest = 5)]
    public bool Exit_sequence_is_idempotent_for_any_call_count(byte seed)
    {
        // Map seed to 1..5 call count
        var callCount = (seed % 5) + 1;

        var transport = new RecordingKenwoodCatTransport { SatelliteStatusOn = true };
        var driver = new KenwoodTs2000Driver(transport, catDelayMs: 0,
            satModeSettlingDelayMs: 0, satModeRetryCount: 3, satModeRetryDelayMs: 0);
        driver.Open();

        // Enter satellite mode first
        driver.SetSatelliteMode(true);
        transport.SentCommands.Clear();

        string[] expectedExitSequence = ["RX;", "TN39;", "TO0;", "TN39;", "SA0010000;"];

        // Call SetSatelliteMode(false) callCount times, verify each produces the same sequence
        for (var i = 0; i < callCount; i++)
        {
            transport.SentCommands.Clear();
            driver.SetSatelliteMode(false);

            var cmds = transport.SentCommands.ToArray();

            if (cmds.Length != expectedExitSequence.Length)
                return false;

            for (var j = 0; j < expectedExitSequence.Length; j++)
            {
                if (cmds[j] != expectedExitSequence[j])
                    return false;
            }
        }

        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Property 4: SA Retry Exhaustion
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Validates: Requirements 2.1, 2.3, 2.4**
    ///
    /// Property 4: SA Retry Exhaustion
    /// ∀ radio-not-confirming scenarios: after satModeRetryCount attempts,
    /// IsSatelliteModeActive == false.
    ///
    /// Uses a NonConfirmingSatelliteTransport (always returns SA0; for SA; queries).
    /// Verifies that SA; count equals retryCount AND IsSatelliteModeActive is false.
    /// </summary>
    [Property(MaxTest = 10)]
    public bool SA_retry_exhaustion_leaves_satellite_mode_inactive(byte seed)
    {
        // Map seed to retry count in range [1, 5]
        var retryCount = (seed % 5) + 1;

        var transport = new NonConfirmingSatelliteTransport();
        var driver = new KenwoodTs2000Driver(transport, catDelayMs: 0,
            satModeSettlingDelayMs: 0, satModeRetryCount: retryCount, satModeRetryDelayMs: 0);
        driver.Open();

        driver.SetSatelliteMode(true);

        // Count how many SA; queries were sent
        var saCount = transport.SentCommands.Count(c => c == "SA;");

        // SA; should appear exactly retryCount times
        if (saCount != retryCount)
            return false;

        // IsSatelliteModeActive must be false after exhaustion
        return !driver.IsSatelliteModeActive;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Property 7: Guard Condition — Not In Sat Mode
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Validates: Requirements 5.3, 8.3, 9.2**
    ///
    /// Property 7: Guard Condition — Not In Sat Mode
    /// ∀ operations requiring satellite mode (DopplerStep, ExchangeVfos, LinkHoldPolls):
    /// when IsSatelliteModeActive == false, zero commands are sent.
    /// </summary>
    [Property(MaxTest = 50)]
    public bool Guard_not_in_sat_mode_sends_zero_commands(byte operationSelector)
    {
        var transport = new RecordingKenwoodCatTransport { SatelliteStatusOn = false };
        var driver = new KenwoodTs2000Driver(transport, catDelayMs: 0,
            satModeSettlingDelayMs: 0, satModeRetryCount: 3, satModeRetryDelayMs: 0);
        driver.Open();

        // Do NOT enter satellite mode — driver should guard all satellite operations
        transport.SentCommands.Clear();

        // Select one of the 3 operations based on selector
        var operation = operationSelector % 3;
        switch (operation)
        {
            case 0:
                driver.ApplySatelliteDopplerStep(145_900_000, 435_700_000);
                break;
            case 1:
                driver.ExchangeVfos();
                break;
            case 2:
                driver.SendSatelliteLinkHoldPolls();
                break;
        }

        // No commands should have been sent
        return transport.SentCommands.Count == 0;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Property 8: Guard Condition — Zero/Negative Frequency
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Validates: Requirements 5.4**
    ///
    /// Property 8: Guard Condition — Zero/Negative Frequency
    /// ∀ hz ≤ 0: ApplySatelliteDopplerStep(hz, _) and ApplySatelliteDopplerStep(_, hz)
    /// return false with no commands sent.
    /// </summary>
    [Property(MaxTest = 50)]
    public bool Guard_zero_or_negative_frequency_sends_no_commands(int seed)
    {
        // Generate a frequency ≤ 0
        var invalidHz = seed <= 0 ? (long)seed : -(long)Math.Abs(seed);
        // Ensure it's truly ≤ 0
        if (invalidHz > 0) invalidHz = 0;

        var validHz = 145_900_000L;

        var transport = new RecordingKenwoodCatTransport { SatelliteStatusOn = true };
        var driver = new KenwoodTs2000Driver(transport, catDelayMs: 0,
            satModeSettlingDelayMs: 0, satModeRetryCount: 3, satModeRetryDelayMs: 0);
        driver.Open();

        // Enter satellite mode
        driver.SetSatelliteMode(true);
        transport.SentCommands.Clear();

        // Test with invalid downlink (first param)
        var result1 = driver.ApplySatelliteDopplerStep(invalidHz, validHz);
        if (result1)
            return false;

        // Test with invalid uplink (second param)
        var result2 = driver.ApplySatelliteDopplerStep(validHz, invalidHz);
        if (result2)
            return false;

        // No commands should have been sent for either call
        return transport.SentCommands.Count == 0;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Private helper transport
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Transport that simulates a radio that NEVER confirms satellite mode.
    /// SA; always returns "SA0;". Used for Property 4 (SA Retry Exhaustion).
    /// Same pattern as in Ts2000SatelliteModeEntryTests.
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
}
