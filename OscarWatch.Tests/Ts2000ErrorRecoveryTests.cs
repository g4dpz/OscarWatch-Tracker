using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

/// <summary>
/// Error recovery tests for <see cref="KenwoodTs2000Driver"/>.
/// Validates graceful degradation when the transport is closed or returns null.
/// Requirements: 13.2, 15.3, 15.4, 15.5
/// </summary>
public sealed class Ts2000ErrorRecoveryTests
{
    /// <summary>
    /// Req 15.3: When IsOpen becomes false, ReadFrequencyHz returns cached value.
    /// After a Doppler step establishes cache, close the transport, then read.
    /// </summary>
    [Fact]
    public void ReadFrequencyHz_returns_cached_when_transport_closed()
    {
        var transport = Ts2000TransportFactory.CreateRecordingTransport();
        var driver = new KenwoodTs2000Driver(
            transport,
            catDelayMs: 0,
            satModeSettlingDelayMs: 0,
            satModeRetryCount: 3,
            satModeRetryDelayMs: 0);
        driver.Open();
        driver.SetSatelliteMode(true);

        var downlinkHz = 145_900_000L;
        var uplinkHz = 435_700_000L;
        driver.ApplySatelliteDopplerStep(downlinkHz, uplinkHz);

        // Close the transport to simulate disconnection
        transport.Dispose();
        Assert.False(transport.IsOpen);

        // ReadFrequencyHz should return the cached values
        var mainHz = driver.ReadFrequencyHz(RigVfo.Main);
        var subHz = driver.ReadFrequencyHz(RigVfo.Sub);

        Assert.Equal(downlinkHz, mainHz);
        Assert.Equal(uplinkHz, subHz);
    }

    /// <summary>
    /// Req 15.4: When transport is closed, SetFrequencyHz caches locally and returns true
    /// without sending any commands.
    /// </summary>
    [Fact]
    public void SetFrequencyHz_caches_locally_when_transport_closed()
    {
        var transport = Ts2000TransportFactory.CreateRecordingTransport();
        var driver = new KenwoodTs2000Driver(
            transport,
            catDelayMs: 0,
            satModeSettlingDelayMs: 0,
            satModeRetryCount: 3,
            satModeRetryDelayMs: 0);
        driver.Open();

        // Close the transport to simulate disconnection
        transport.Dispose();
        Assert.False(transport.IsOpen);

        // Clear any commands from Open/SetSatelliteMode
        transport.SentCommands.Clear();

        // SetFrequencyHz should cache and return true without sending commands
        var result = driver.SetFrequencyHz(145_900_000L);

        Assert.True(result);
        Assert.Empty(transport.SentCommands);
    }

    /// <summary>
    /// Req 15.5: When transport is closed, SetSatelliteMode sets the internal flag
    /// without sending commands.
    /// </summary>
    [Fact]
    public void SetSatelliteMode_sets_flag_when_transport_closed()
    {
        var transport = Ts2000TransportFactory.CreateRecordingTransport();
        var driver = new KenwoodTs2000Driver(
            transport,
            catDelayMs: 0,
            satModeSettlingDelayMs: 0,
            satModeRetryCount: 3,
            satModeRetryDelayMs: 0);
        driver.Open();

        // Close the transport to simulate disconnection
        transport.Dispose();
        Assert.False(transport.IsOpen);

        // Clear any commands from Open
        transport.SentCommands.Clear();

        // SetSatelliteMode should set internal flag without sending commands
        driver.SetSatelliteMode(true);

        Assert.True(driver.IsSatelliteModeActive);
        Assert.Empty(transport.SentCommands);
    }

    /// <summary>
    /// Req 13.2: When Transact returns null (or a zero-frequency reply), the driver uses
    /// the cached frequency value. After a Doppler step establishes cache, change the
    /// transport state so that Transact returns a zero frequency, then ReadFrequencyHz
    /// should return the cached value.
    /// </summary>
    [Fact]
    public void ReadFrequencyHz_uses_cached_frequency_when_transact_returns_null()
    {
        var transport = Ts2000TransportFactory.CreateRecordingTransport();
        var driver = new KenwoodTs2000Driver(
            transport,
            catDelayMs: 0,
            satModeSettlingDelayMs: 0,
            satModeRetryCount: 3,
            satModeRetryDelayMs: 0);
        driver.Open();
        driver.SetSatelliteMode(true);

        var downlinkHz = 145_900_000L;
        var uplinkHz = 435_700_000L;
        driver.ApplySatelliteDopplerStep(downlinkHz, uplinkHz);

        // Simulate Transact returning an unusable reply by setting FaHz/FbHz to 0.
        // The recording transport will return FA00000000000; which parses to 0 Hz,
        // triggering the driver's fallback to cached values (same as null behavior).
        transport.FaHz = 0;
        transport.FbHz = 0;

        var mainHz = driver.ReadFrequencyHz(RigVfo.Main);
        var subHz = driver.ReadFrequencyHz(RigVfo.Sub);

        Assert.Equal(downlinkHz, mainHz);
        Assert.Equal(uplinkHz, subHz);
    }
}
