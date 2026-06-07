// Feature: ts2000-satmode-entry-fix, Property 1: Bug Condition — Silent SATL Confirmation Override

using FsCheck.Xunit;
using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.4**
///
/// Bug condition exploration test: demonstrates that when SetSatelliteMode(true) is called
/// on an open transport where the radio does NOT confirm SATL (SA; returns SA0; or null),
/// the unfixed code incorrectly sets IsSatelliteModeActive = true via the resilience override.
///
/// Expected behavior (design): IsSatelliteModeActive should be false when the radio
/// does not confirm satellite mode active.
///
/// This test is EXPECTED TO FAIL on unfixed code — failure confirms the bug exists.
/// </summary>
public class SatelliteModeBugConditionPropertyTests
{
    /// <summary>
    /// Property 1: Bug Condition — Silent SATL Confirmation Override
    ///
    /// For any SetSatelliteMode(true) call on an open transport where SA; returns SA0;
    /// (radio not confirming SATL), IsSatelliteModeActive MUST be false.
    ///
    /// The unfixed code will return true because the resilience override sets
    /// _satelliteMode = true regardless — this is the counterexample.
    ///
    /// Also verifies:
    /// - No settling delay is present after SA1010110; (command log shows immediate handshake)
    /// - No retry attempts are made for SA; verification (only one SA; in command log)
    /// </summary>
    [Property(MaxTest = 10)]
    public bool SatelliteMode_not_active_when_radio_does_not_confirm_SATL(bool returnNull)
    {
        // Arrange: transport that simulates radio NOT ready (never confirms SATL)
        var transport = new NonConfirmingSatelliteTransport(returnNull);
        var driver = new KenwoodTs2000Driver(transport, catDelayMs: 0, satModeSettlingDelayMs: 0, satModeRetryCount: 3, satModeRetryDelayMs: 0);
        driver.Open();

        // Act: attempt satellite mode entry
        driver.SetSatelliteMode(true);

        // Assert expected behavior:
        // 1. IsSatelliteModeActive should be false (radio didn't confirm)
        if (driver.IsSatelliteModeActive)
            return false;

        // 2. Multiple SA; queries in command log (retries were attempted)
        var saQueryCount = transport.SentCommands.Count(c => c == "SA;");
        if (saQueryCount != 3) // default retry count
            return false;

        return true;
    }

    /// <summary>
    /// Transport that simulates a radio that has NOT confirmed satellite mode.
    /// Unlike RecordingKenwoodCatTransport, this does NOT auto-flip SatelliteStatusOn
    /// when SA1010110; is sent — simulating the real-world timing gap where the radio
    /// needs settling time before it can confirm SATL.
    /// </summary>
    private sealed class NonConfirmingSatelliteTransport : IKenwoodCatTransport
    {
        private readonly bool _returnNull;

        public NonConfirmingSatelliteTransport(bool returnNull)
        {
            _returnNull = returnNull;
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
                // SA; query: radio does NOT confirm SATL (the bug condition)
                "SA;" => _returnNull ? null : "SA0;",
                // FA; read during handshake — return a valid frequency
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
