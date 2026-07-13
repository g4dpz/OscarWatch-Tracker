// Feature: ts2000-hardware-validation, Property 6: Link-hold poll rate

using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 9.1**
///
/// Property 6: SendSatelliteLinkHoldPollNow sends exactly one FA; per call in satellite mode.
/// </summary>
public class Ts2000LinkHoldPollCountPropertyTests
{
    [Property(MaxTest = 20)]
    public bool SendSatelliteLinkHoldPollNow_sends_exactly_one_FA_command(byte unusedSeed)
    {
        var transport = new RecordingKenwoodCatTransport { SatelliteStatusOn = true };
        var driver = new KenwoodTs2000Driver(transport, catDelayMs: 0);
        driver.Open();
        driver.SetSatelliteMode(true);
        transport.SentCommands.Clear();

        driver.SendSatelliteLinkHoldPollNow();

        var cmds = transport.SentCommands;
        if (cmds.Count != 1)
            return false;

        return cmds[0] == "FA;";
    }
}
