using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

/// <summary>
/// Validates VFO exchange behaviour: reading FA/FB then writing swapped values,
/// cached frequency updates, guard conditions, and abort on invalid reads.
/// Validates: Requirements 8.1, 8.2, 8.3, 8.4
/// </summary>
public class Ts2000VfoExchangeTests : Ts2000TestBase
{
    /// <summary>
    /// Requirement 8.1: ExchangeVfos reads current FA and FB values, then writes
    /// the former FB value to FA and the former FA value to FB.
    /// Default transport: FaHz=435_750_000, FbHz=145_900_000.
    /// After exchange: FA command = "FA00145900000;" (former FB), FB command = "FB00435750000;" (former FA).
    /// </summary>
    [Fact]
    public void ExchangeVfos_reads_FA_FB_then_writes_swapped()
    {
        EnterSatelliteMode();
        ClearCommandLog();

        Driver.ExchangeVfos();

        var cmds = GetSentCommands();

        // Should have 4 commands: FA; read, FB; read, FA write (former FB), FB write (former FA)
        Assert.Equal(4, cmds.Count);
        Assert.Equal("FA;", cmds[0]);
        Assert.Equal("FB;", cmds[1]);
        Assert.Equal("FA00145900000;", cmds[2]); // former FB value written to FA
        Assert.Equal("FB00435750000;", cmds[3]); // former FA value written to FB
    }

    /// <summary>
    /// Requirement 8.2: After ExchangeVfos completes, internal cached frequencies
    /// are swapped so that Main Hz = former Sub and Sub Hz = former Main.
    /// Verify by calling ReadFrequencyHz after exchange (transport also has updated values).
    /// </summary>
    [Fact]
    public void ExchangeVfos_updates_cached_frequencies()
    {
        EnterSatelliteMode();

        Driver.ExchangeVfos();

        // After exchange, the recording transport's FaHz/FbHz have been updated by ApplySetFrequency:
        // FA was written with 145_900_000, FB was written with 435_750_000.
        // ReadFrequencyHz reads from transport which now reflects the swapped values.
        var mainHz = Driver.ReadFrequencyHz(RigVfo.Main);
        var subHz = Driver.ReadFrequencyHz(RigVfo.Sub);

        // Main (FA) should now be the former Sub value (145_900_000)
        Assert.Equal(145_900_000L, mainHz);
        // Sub (FB) should now be the former Main value (435_750_000)
        Assert.Equal(435_750_000L, subHz);
    }

    /// <summary>
    /// Requirement 8.3: ExchangeVfos sends no commands when not in satellite mode.
    /// </summary>
    [Fact]
    public void ExchangeVfos_no_commands_when_not_in_satellite_mode()
    {
        // Do NOT call EnterSatelliteMode() — driver is in normal VFO mode
        ClearCommandLog();

        Driver.ExchangeVfos();

        Assert.Empty(GetSentCommands());
    }

    /// <summary>
    /// Requirement 8.4: ExchangeVfos aborts and sends no write commands when
    /// the frequency read returns zero (transport FaHz set to 0 causes ReadFrequencyHz
    /// to return null since parsed hz &lt;= 0 and no cached value exists).
    /// </summary>
    [Fact]
    public void ExchangeVfos_aborts_when_frequency_read_returns_zero()
    {
        EnterSatelliteMode();

        // Set FaHz to 0 so that FA; reply parses to 0, triggering null return
        RecordingTransport!.FaHz = 0;
        ClearCommandLog();

        Driver.ExchangeVfos();

        var cmds = GetSentCommands();

        // FA; read is sent, but the result is null/zero so exchange aborts.
        // No FB; read or write commands should follow.
        Assert.Contains("FA;", cmds);
        AssertNoCommandStartingWith("FA0");
        AssertNoCommandStartingWith("FB0");
    }
}
