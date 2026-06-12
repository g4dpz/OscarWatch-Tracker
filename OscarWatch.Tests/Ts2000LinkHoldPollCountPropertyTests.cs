// Feature: ts2000-hardware-validation, Property 6: Link-Hold Poll Count

using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 9.1**
///
/// Property 6: Link-Hold Poll Count
/// ∀ calls to SendSatelliteLinkHoldPolls in satellite mode: exactly 7 FA; commands sent via Transact.
/// </summary>
public class Ts2000LinkHoldPollCountPropertyTests
{
    /// <summary>
    /// Property 6: For any call to SendSatelliteLinkHoldPolls when the driver is in satellite mode,
    /// exactly 7 FA; commands are sent via Transact.
    /// </summary>
    [Property(MaxTest = 50)]
    public bool SendSatelliteLinkHoldPolls_sends_exactly_7_FA_commands(byte unusedSeed)
    {
        var transport = new RecordingKenwoodCatTransport { SatelliteStatusOn = true };
        var driver = new KenwoodTs2000Driver(transport, catDelayMs: 0);
        driver.Open();
        driver.SetSatelliteMode(true);
        transport.SentCommands.Clear();

        driver.SendSatelliteLinkHoldPolls();

        var cmds = transport.SentCommands;

        // Must be exactly 7 commands
        if (cmds.Count != 7)
            return false;

        // All must be "FA;"
        for (var i = 0; i < 7; i++)
        {
            if (cmds[i] != "FA;")
                return false;
        }

        return true;
    }
}
