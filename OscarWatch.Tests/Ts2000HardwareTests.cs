using OscarWatch.Rig;

namespace OscarWatch.Tests;

/// <summary>
/// Hardware validation tests for the TS-2000. These tests run against real hardware
/// via the serial transport and are skipped when <c>TS2000_COM_PORT</c> is not configured.
/// </summary>
[Trait("Category", "Hardware")]
public sealed class Ts2000HardwareTests : IDisposable
{
    private KenwoodCatTransport? _transport;
    private KenwoodTs2000Driver? _driver;

    /// <summary>
    /// Configurable inter-command delay from <c>TS2000_CAT_DELAY_MS</c> environment variable.
    /// </summary>
    private static int CatDelayMs => Ts2000TransportFactory.CatDelayMs;

    private KenwoodTs2000Driver CreateDriverAndOpen()
    {
        _transport = Ts2000TransportFactory.CreateSerialTransport();
        _driver = new KenwoodTs2000Driver(
            _transport,
            catDelayMs: CatDelayMs,
            satModeSettlingDelayMs: 250,
            satModeRetryCount: 3,
            satModeRetryDelayMs: 200);
        _driver.Open();
        return _driver;
    }

    [SkippableFact]
    public void Hardware_satellite_mode_entry_succeeds()
    {
        Skip.If(!Ts2000TransportFactory.IsHardwareAvailable, "TS2000_COM_PORT not configured");

        var driver = CreateDriverAndOpen();
        try
        {
            driver.SetSatelliteMode(true);

            Assert.True(driver.IsSatelliteModeActive);
        }
        finally
        {
            driver.SetSatelliteMode(false);
        }
    }

    [SkippableFact]
    public void Hardware_doppler_step_readback_matches_commanded()
    {
        Skip.If(!Ts2000TransportFactory.IsHardwareAvailable, "TS2000_COM_PORT not configured");

        var driver = CreateDriverAndOpen();
        try
        {
            driver.SetSatelliteMode(true);
            Assert.True(driver.IsSatelliteModeActive);

            var downlinkHz = 145_900_000L;
            var uplinkHz = 435_700_000L;

            var applied = driver.ApplySatelliteDopplerStep(downlinkHz, uplinkHz);
            Assert.True(applied);

            var readMainHz = driver.ReadFrequencyHz(RigVfo.Main);
            var readSubHz = driver.ReadFrequencyHz(RigVfo.Sub);

            Assert.NotNull(readMainHz);
            Assert.NotNull(readSubHz);
            Assert.Equal(downlinkHz, readMainHz!.Value);
            Assert.Equal(uplinkHz, readSubHz!.Value);
        }
        finally
        {
            driver.SetSatelliteMode(false);
        }
    }

    [SkippableFact]
    public void Hardware_satellite_mode_exit_succeeds()
    {
        Skip.If(!Ts2000TransportFactory.IsHardwareAvailable, "TS2000_COM_PORT not configured");

        var driver = CreateDriverAndOpen();
        try
        {
            driver.SetSatelliteMode(true);
            Assert.True(driver.IsSatelliteModeActive);
        }
        finally
        {
            driver.SetSatelliteMode(false);
        }

        Assert.False(driver.IsSatelliteModeActive);
    }

    public void Dispose()
    {
        // Always attempt to exit satellite mode on teardown
        if (_driver?.IsSatelliteModeActive == true)
        {
            try
            {
                _driver.SetSatelliteMode(false);
            }
            catch
            {
                // Best effort — don't throw from Dispose
            }
        }

        _transport?.Dispose();
    }
}
