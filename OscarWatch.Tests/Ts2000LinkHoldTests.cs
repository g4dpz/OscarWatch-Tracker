using OscarWatch.Rig;

namespace OscarWatch.Tests;

/// <summary>
/// Validates link-hold polling behaviour: exactly 7 FA; commands sent via Transact
/// in satellite mode, and guard conditions that prevent commands when not in satellite
/// mode or when the transport is not open.
/// Validates: Requirements 9.1, 9.2, 9.3, 9.4
/// </summary>
public class Ts2000LinkHoldTests : Ts2000TestBase
{
    /// <summary>
    /// Requirement 9.1: SendSatelliteLinkHoldPolls sends exactly 7 FA; commands
    /// using Transact when satellite mode is active and transport is open.
    /// </summary>
    [Fact]
    public void LinkHoldPolls_sends_exactly_7_FA_commands_in_satellite_mode()
    {
        EnterSatelliteMode();
        ClearCommandLog();

        Driver.SendSatelliteLinkHoldPolls();

        var cmds = GetSentCommands();
        Assert.Equal(7, cmds.Count);
        Assert.All(cmds, cmd => Assert.Equal("FA;", cmd));
    }

    /// <summary>
    /// Requirement 9.2: SendSatelliteLinkHoldPolls sends no commands when
    /// the driver is not in satellite mode.
    /// </summary>
    [Fact]
    public void LinkHoldPolls_sends_no_commands_when_not_in_satellite_mode()
    {
        // Do NOT call EnterSatelliteMode() — driver is in normal VFO mode
        ClearCommandLog();

        Driver.SendSatelliteLinkHoldPolls();

        Assert.Empty(GetSentCommands());
    }

    /// <summary>
    /// Requirement 9.3: SendSatelliteLinkHoldPolls sends no commands when
    /// the transport is not open, even if satellite mode flag is set.
    /// </summary>
    [Fact]
    public void LinkHoldPolls_sends_no_commands_when_transport_not_open()
    {
        // Create a separate driver with a recording transport that is NOT opened.
        // SetSatelliteMode(true) with closed transport sets _satelliteMode = true
        // without serial I/O, then verify no commands are sent.
        var transport = Ts2000TransportFactory.CreateRecordingTransport();
        var driver = new KenwoodTs2000Driver(
            transport,
            catDelayMs: 0,
            satModeSettlingDelayMs: 0,
            satModeRetryCount: 3,
            satModeRetryDelayMs: 0);

        // Don't call driver.Open() — transport remains closed
        driver.SetSatelliteMode(true); // Sets internal _satelliteMode = true without I/O

        transport.SentCommands.Clear();

        driver.SendSatelliteLinkHoldPolls();

        Assert.Empty(transport.SentCommands);

        driver.Dispose();
    }
}
