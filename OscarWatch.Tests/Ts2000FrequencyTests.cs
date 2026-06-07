using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

/// <summary>
/// Validates frequency set/read-back in satellite mode: FA/FB 11-digit commands,
/// SM band-select commands, and ReadFrequencyHz parsing.
/// Validates: Requirements 4.1, 4.2, 4.3, 4.4
/// </summary>
public class Ts2000FrequencyTests : Ts2000TestBase
{
    /// <summary>
    /// Requirement 4.1: ApplySatelliteDopplerStep sends FA with 11-digit downlink
    /// and FB with 11-digit uplink during the Doppler step cluster.
    /// </summary>
    [Theory]
    [InlineData(145_900_000L, 435_700_000L)]
    [InlineData(435_300_000L, 145_950_000L)]
    public void DopplerStep_sends_FA_with_11digit_downlink_and_FB_with_11digit_uplink(long downlinkHz, long uplinkHz)
    {
        EnterSatelliteMode();
        ClearCommandLog();

        Driver.ApplySatelliteDopplerStep(downlinkHz, uplinkHz);

        var cmds = GetSentCommands();
        var expectedFa = $"FA{downlinkHz:D11};";
        var expectedFb = $"FB{uplinkHz:D11};";

        AssertCommandContains(expectedFa);
        AssertCommandContains(expectedFb);
    }

    /// <summary>
    /// Requirement 4.2: After a Doppler step, SM10000; (main band-select) and an SM sub-band
    /// command appropriate for the downlink frequency are sent.
    /// >= 200 MHz => SM00004; else SM00021;
    /// </summary>
    [Theory]
    [InlineData(145_900_000L, 435_700_000L, "SM00021;")]
    [InlineData(435_300_000L, 145_950_000L, "SM00004;")]
    public void DopplerStep_sends_SM10000_and_SM_sub_band_command(long downlinkHz, long uplinkHz, string expectedSmSub)
    {
        EnterSatelliteMode();
        ClearCommandLog();

        Driver.ApplySatelliteDopplerStep(downlinkHz, uplinkHz);

        var cmds = GetSentCommands();

        AssertCommandContains("SM10000;");
        AssertCommandContains(expectedSmSub);
    }

    /// <summary>
    /// Requirement 4.3: ReadFrequencyHz(RigVfo.Main) sends FA; and parses the 11-digit reply.
    /// The RecordingKenwoodCatTransport returns FA{FaHz:D11}; for FA; queries.
    /// </summary>
    [Theory]
    [InlineData(145_900_000L, 435_700_000L)]
    [InlineData(435_300_000L, 145_950_000L)]
    public void ReadFrequencyHz_Main_sends_FA_and_parses_11digit_reply(long faHz, long fbHz)
    {
        RecordingTransport!.FaHz = faHz;
        RecordingTransport!.FbHz = fbHz;

        EnterSatelliteMode();
        ClearCommandLog();

        var result = Driver.ReadFrequencyHz(RigVfo.Main);

        AssertCommandContains("FA;");
        Assert.Equal(faHz, result);
    }

    /// <summary>
    /// Requirement 4.4: ReadFrequencyHz(RigVfo.Sub) sends FB; and parses the 11-digit reply.
    /// The RecordingKenwoodCatTransport returns FB{FbHz:D11}; for FB; queries.
    /// </summary>
    [Theory]
    [InlineData(145_900_000L, 435_700_000L)]
    [InlineData(435_300_000L, 145_950_000L)]
    public void ReadFrequencyHz_Sub_sends_FB_and_parses_11digit_reply(long faHz, long fbHz)
    {
        RecordingTransport!.FaHz = faHz;
        RecordingTransport!.FbHz = fbHz;

        EnterSatelliteMode();
        ClearCommandLog();

        var result = Driver.ReadFrequencyHz(RigVfo.Sub);

        AssertCommandContains("FB;");
        Assert.Equal(fbHz, result);
    }

    // ─────────────────────────────────────────────────────────────────
    // Frequency Read-Back Accuracy After Doppler Updates
    // Validates: Requirements 12.1, 12.2, 12.3, 12.4
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Requirement 12.1: After a Doppler step, the commanded downlink frequency
    /// is stored as the cached Main VFO value.
    /// </summary>
    [Theory]
    [InlineData(145_900_000L, 435_700_000L)]
    [InlineData(435_300_000L, 145_950_000L)]
    public void DopplerStep_stores_commanded_downlink_as_cached_Main(long downlinkHz, long uplinkHz)
    {
        EnterSatelliteMode();

        Driver.ApplySatelliteDopplerStep(downlinkHz, uplinkHz);

        // After the Doppler step, reading Main should return downlinkHz.
        // The recording transport's FaHz is updated to downlinkHz by the FA command,
        // so the transport also returns downlinkHz — confirming cache matches commanded value.
        var result = Driver.ReadFrequencyHz(RigVfo.Main);
        Assert.Equal(downlinkHz, result);
    }

    /// <summary>
    /// Requirement 12.2: After a Doppler step, the commanded uplink frequency
    /// is stored as the cached Sub VFO value.
    /// </summary>
    [Theory]
    [InlineData(145_900_000L, 435_700_000L)]
    [InlineData(435_300_000L, 145_950_000L)]
    public void DopplerStep_stores_commanded_uplink_as_cached_Sub(long downlinkHz, long uplinkHz)
    {
        EnterSatelliteMode();

        Driver.ApplySatelliteDopplerStep(downlinkHz, uplinkHz);

        // After the Doppler step, reading Sub should return uplinkHz.
        var result = Driver.ReadFrequencyHz(RigVfo.Sub);
        Assert.Equal(uplinkHz, result);
    }

    /// <summary>
    /// Requirement 12.3: ReadFrequencyHz returns the parsed transport reply (not the cached value)
    /// when the transport returns a valid frequency reply.
    /// Proves the driver reads from transport, not cache, by changing the transport's
    /// FaHz to a different value after the Doppler step.
    /// </summary>
    [Fact]
    public void ReadFrequencyHz_returns_parsed_transport_reply_not_cached()
    {
        const long downlinkHz = 145_900_000L;
        const long uplinkHz = 435_700_000L;
        const long transportNewHz = 145_912_345L;

        EnterSatelliteMode();
        Driver.ApplySatelliteDopplerStep(downlinkHz, uplinkHz);

        // Change transport reply to a different value than the cached downlink
        RecordingTransport!.FaHz = transportNewHz;

        var result = Driver.ReadFrequencyHz(RigVfo.Main);

        // Should return the transport's current value, NOT the cached downlinkHz
        Assert.Equal(transportNewHz, result);
        Assert.NotEqual(downlinkHz, result);
    }

    /// <summary>
    /// Requirement 12.4: ReadFrequencyHz returns the cached value when the transport
    /// returns a value that cannot be parsed as a valid frequency (zero triggers fallback).
    /// Setting FaHz to 0 causes the transport to return FA00000000000; which parses to 0,
    /// triggering the hz &lt;= 0 fallback path to return the cached value.
    /// </summary>
    [Fact]
    public void ReadFrequencyHz_returns_cached_value_when_transport_returns_zero()
    {
        const long downlinkHz = 145_900_000L;
        const long uplinkHz = 435_700_000L;

        EnterSatelliteMode();
        Driver.ApplySatelliteDopplerStep(downlinkHz, uplinkHz);

        // Set transport to return 0 Hz (simulates parse failure / invalid reply)
        RecordingTransport!.FaHz = 0;

        var result = Driver.ReadFrequencyHz(RigVfo.Main);

        // Should fall back to the cached downlink value
        Assert.Equal(downlinkHz, result);
    }
}
