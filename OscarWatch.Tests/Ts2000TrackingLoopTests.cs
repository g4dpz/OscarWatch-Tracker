using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

/// <summary>
/// Integration tests validating the RigController → KenwoodTs2000Driver tracking loop interaction.
/// Uses a KenwoodTs2000Driver backed by RecordingKenwoodCatTransport so that command sequences
/// can be asserted while exercising the real controller logic.
///
/// Requirements: 11.1, 11.2, 11.3, 11.4
/// </summary>
public class Ts2000TrackingLoopTests
{
    /// <summary>
    /// Requirement 11.1: When RunTrackingLoopOnce executes with an active satellite context,
    /// the RigController calls ApplySatelliteDopplerStep on the Driver with computed frequencies.
    /// </summary>
    [Fact]
    public void RunTrackingLoopOnce_calls_ApplySatelliteDopplerStep_with_correct_frequencies()
    {
        var transport = Ts2000TransportFactory.CreateRecordingTransport();
        var driver = new KenwoodTs2000Driver(
            transport, catDelayMs: 0, satModeSettlingDelayMs: 0,
            satModeRetryCount: 3, satModeRetryDelayMs: 0);
        var controller = new RigController(_ => driver);

        var mode = new SatelliteTransponderMode
        {
            Type = "FM VOICE",
            DownlinkKHz = 145_900,
            UplinkKHz = 435_700,
            DownlinkMode = "FMN",
            UplinkMode = "FMN",
            Doppler = "NOR"
        };

        var state = new SatelliteTrackState
        {
            Name = "IO-117",
            NoradId = "55555",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(180, 45, 600, 2.0)
        };

        // Initial pass with range rate 2.0 km/s
        var corrected1 = DopplerFrequencyCalculator.Compute(mode, 2.0, 0);
        var ctx1 = new RigTrackingContext
        {
            TrackState = state,
            Mode = mode,
            Corrected = corrected1
        };

        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.KenwoodTs2000,
            Port = "COM1",
            DopplerThresholdFmHz = 200,
            DopplerThresholdLinearHz = 50,
            CatDelayMs = 0
        };

        // First Update enters satellite mode and programs pass frequencies
        controller.Update(settings, ctx1);
        controller.DrainCommandQueueForTests();
        Assert.True(driver.IsSatelliteModeActive);

        // Now change the Doppler context significantly (range rate changes from 2.0 to 5.0)
        // so the frequency delta exceeds the FM threshold of 200 Hz.
        // The RigController recomputes Doppler from LookAngles.RangeRateKmPerSec, not Corrected.
        var state2 = new SatelliteTrackState
        {
            Name = "IO-117",
            NoradId = "55555",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(180, 45, 600, 5.0)
        };

        var corrected2 = DopplerFrequencyCalculator.Compute(mode, 5.0, 0);
        var ctx2 = new RigTrackingContext
        {
            TrackState = state2,
            Mode = mode,
            Corrected = corrected2
        };

        // Publish the new context so the tracking loop uses the updated range rate
        controller.PublishContext(settings, ctx2);
        controller.DrainCommandQueueForTests();

        // Clear command log to isolate the tracking loop iteration
        transport.SentCommands.Clear();

        // Now trigger a tracking loop iteration — this should call ApplySatelliteDopplerStep
        controller.RunTrackingLoopOnce();
        controller.DrainCommandQueueForTests();

        var cmds = transport.SentCommands;

        // The Doppler step cluster should contain FA (downlink) and FB (uplink) commands
        var expectedRxHz = (long)Math.Round(corrected2.RadioReceiveKHz * 1000.0);
        var expectedTxHz = (long)Math.Round(corrected2.RadioTransmitKHz * 1000.0);

        var faCmd = $"FA{expectedRxHz:D11};";
        var fbCmd = $"FB{expectedTxHz:D11};";

        Assert.Contains(faCmd, cmds);
        Assert.Contains(fbCmd, cmds);
    }

    /// <summary>
    /// Requirement 11.2: The RigController only calls ApplySatelliteDopplerStep when the computed
    /// frequency change exceeds the write threshold. Small changes produce no commands.
    /// </summary>
    [Fact]
    public void Tracking_loop_gates_on_frequency_threshold_small_changes_produce_no_commands()
    {
        var transport = Ts2000TransportFactory.CreateRecordingTransport();
        var driver = new KenwoodTs2000Driver(
            transport, catDelayMs: 0, satModeSettlingDelayMs: 0,
            satModeRetryCount: 3, satModeRetryDelayMs: 0);
        var controller = new RigController(_ => driver);

        var mode = new SatelliteTransponderMode
        {
            Type = "FM VOICE",
            DownlinkKHz = 145_900,
            UplinkKHz = 435_700,
            DownlinkMode = "FMN",
            UplinkMode = "FMN",
            Doppler = "NOR"
        };

        var state = new SatelliteTrackState
        {
            Name = "IO-117",
            NoradId = "55555",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(180, 45, 600, 2.0)
        };

        var corrected = DopplerFrequencyCalculator.Compute(mode, 2.0, 0);
        var ctx = new RigTrackingContext
        {
            TrackState = state,
            Mode = mode,
            Corrected = corrected
        };

        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.KenwoodTs2000,
            Port = "COM1",
            DopplerThresholdFmHz = 200,
            DopplerThresholdLinearHz = 50,
            CatDelayMs = 0
        };

        // First Update runs pass init + initial frequency write
        controller.Update(settings, ctx);
        controller.DrainCommandQueueForTests();

        // Now apply the same context with the same Doppler shift — no threshold exceeded
        transport.SentCommands.Clear();
        controller.RunTrackingLoopOnce();
        controller.DrainCommandQueueForTests();

        // No FA/FB frequency-set commands should be sent since frequencies haven't changed
        // enough to exceed the threshold. FA; read commands (link-hold polls) are OK but
        // FA with 11 digits (set commands) should not appear.
        var cmds = transport.SentCommands;
        var faWriteCommands = cmds.Where(c => c.StartsWith("FA0") && c.Length == 14).ToList();
        var fbWriteCommands = cmds.Where(c => c.StartsWith("FB0") && c.Length == 14).ToList();

        Assert.Empty(faWriteCommands);
        Assert.Empty(fbWriteCommands);
    }

    /// <summary>
    /// Requirement 11.3: When the satellite pass context changes to a new pass, the RigController
    /// calls ApplySatellitePassFrequencies to reinitialise the radio before Doppler tracking begins.
    /// </summary>
    [Fact]
    public void New_pass_context_calls_ApplySatellitePassFrequencies()
    {
        var transport = Ts2000TransportFactory.CreateRecordingTransport();
        var driver = new KenwoodTs2000Driver(
            transport, catDelayMs: 0, satModeSettlingDelayMs: 0,
            satModeRetryCount: 3, satModeRetryDelayMs: 0);
        var controller = new RigController(_ => driver);

        var mode1 = new SatelliteTransponderMode
        {
            Type = "FM VOICE",
            DownlinkKHz = 145_900,
            UplinkKHz = 435_700,
            DownlinkMode = "FMN",
            UplinkMode = "FMN",
            Doppler = "NOR"
        };

        var state1 = new SatelliteTrackState
        {
            Name = "IO-117",
            NoradId = "55555",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(180, 45, 600, 2.0)
        };

        var corrected1 = DopplerFrequencyCalculator.Compute(mode1, 2.0, 0);
        var ctx1 = new RigTrackingContext
        {
            TrackState = state1,
            Mode = mode1,
            Corrected = corrected1
        };

        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.KenwoodTs2000,
            Port = "COM1",
            DopplerThresholdFmHz = 200,
            DopplerThresholdLinearHz = 50,
            CatDelayMs = 0
        };

        // First pass
        controller.Update(settings, ctx1);
        controller.DrainCommandQueueForTests();
        Assert.True(driver.IsSatelliteModeActive);

        // Switch to a different satellite (new pass context, different NoradId)
        var mode2 = new SatelliteTransponderMode
        {
            Type = "Linear",
            DownlinkKHz = 435_300,
            UplinkKHz = 145_950,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        var state2 = new SatelliteTrackState
        {
            Name = "RS-44",
            NoradId = "44909",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(180, 30, 700, 1.5)
        };

        var corrected2 = DopplerFrequencyCalculator.Compute(mode2, 1.5, 0);
        var ctx2 = new RigTrackingContext
        {
            TrackState = state2,
            Mode = mode2,
            Corrected = corrected2
        };

        transport.SentCommands.Clear();

        // Update with a new pass context
        controller.Update(settings, ctx2);
        controller.DrainCommandQueueForTests();

        var cmds = transport.SentCommands;

        // Pass init for TS-2000 calls ApplySatellitePassFrequencies which programs
        // frequencies via double FA/FB, SM, mode commands, PC050, AI0, and link-hold polls.
        // Verify that the new downlink frequency appears in FA commands.
        var expectedRxHz = (long)Math.Round(corrected2.RadioReceiveKHz * 1000.0);
        var faCmd = $"FA{expectedRxHz:D11};";
        Assert.Contains(faCmd, cmds);

        // PC050 is part of ApplySatellitePassFrequencies
        Assert.Contains("PC050;", cmds);

        // AI0 finalises the pass programming
        Assert.Contains("AI0;", cmds);
    }

    /// <summary>
    /// Requirement 11.4: When the tracking context is cleared (pass ends), the RigController
    /// calls SetSatelliteMode(false) to exit satellite mode on the Driver.
    /// In practice, this happens when a new pass starts that doesn't require satellite mode
    /// (e.g., a beacon-only pass), causing the controller to exit sat mode.
    /// </summary>
    [Fact]
    public void New_non_satellite_pass_calls_SetSatelliteMode_false()
    {
        var transport = Ts2000TransportFactory.CreateRecordingTransport();
        var driver = new KenwoodTs2000Driver(
            transport, catDelayMs: 0, satModeSettlingDelayMs: 0,
            satModeRetryCount: 3, satModeRetryDelayMs: 0);
        var controller = new RigController(_ => driver);

        var mode = new SatelliteTransponderMode
        {
            Type = "FM VOICE",
            DownlinkKHz = 145_900,
            UplinkKHz = 435_700,
            DownlinkMode = "FMN",
            UplinkMode = "FMN",
            Doppler = "NOR"
        };

        var state = new SatelliteTrackState
        {
            Name = "IO-117",
            NoradId = "55555",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(180, 45, 600, 2.0)
        };

        var corrected = DopplerFrequencyCalculator.Compute(mode, 2.0, 0);
        var ctx = new RigTrackingContext
        {
            TrackState = state,
            Mode = mode,
            Corrected = corrected
        };

        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.KenwoodTs2000,
            Port = "COM1",
            DopplerThresholdFmHz = 200,
            DopplerThresholdLinearHz = 50,
            CatDelayMs = 0
        };

        // Enter tracking in satellite mode (cross-band V/U uses main/sub)
        controller.Update(settings, ctx);
        controller.DrainCommandQueueForTests();
        Assert.True(driver.IsSatelliteModeActive);

        // Now switch to a beacon-only satellite (no uplink — this triggers SetSatelliteMode(false))
        var beaconMode = new SatelliteTransponderMode
        {
            Type = "Beacon",
            DownlinkKHz = 145_825,
            UplinkKHz = 0,
            DownlinkMode = "FMN",
            UplinkMode = "",
            Doppler = "NOR"
        };

        var beaconState = new SatelliteTrackState
        {
            Name = "ISS-Beacon",
            NoradId = "25544",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(180, 30, 400, 1.0)
        };

        var beaconCorrected = DopplerFrequencyCalculator.Compute(beaconMode, 1.0, 0);
        var beaconCtx = new RigTrackingContext
        {
            TrackState = beaconState,
            Mode = beaconMode,
            Corrected = beaconCorrected
        };

        transport.SentCommands.Clear();

        // Switching to beacon pass should exit satellite mode
        controller.Update(settings, beaconCtx);
        controller.DrainCommandQueueForTests();

        // The driver should no longer be in satellite mode
        Assert.False(driver.IsSatelliteModeActive);

        // The exit sequence should have been sent (SA0010000; is the final command in exit)
        Assert.Contains("SA0010000;", transport.SentCommands);
    }
}
