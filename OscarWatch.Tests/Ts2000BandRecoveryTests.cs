using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

/// <summary>
/// TS-2000 SAT band-layout recovery when FA/FB programming fails after satellite change.
/// </summary>
public sealed class Ts2000BandRecoveryTests : Ts2000TestBase
{
    private const long UhfDownlinkHz = 435_850_000;
    private const long VhfUplinkHz = 145_952_000;

    [Fact]
    public void PassFrequenciesWithBandRecovery_skips_recovery_when_initial_programming_succeeds()
    {
        EnterSatelliteMode();
        ClearCommandLog();

        var ok = Driver.ApplySatellitePassFrequenciesWithBandRecovery(
            UhfDownlinkHz,
            VhfUplinkHz,
            435_850.45,
            '2',
            '1');

        Assert.True(ok);
        Assert.DoesNotContain("SA0010000;", GetSentCommands());
        Assert.Equal(UhfDownlinkHz, RecordingTransport.FaHz);
        Assert.Equal(VhfUplinkHz, RecordingTransport.FbHz);
    }

    [Fact]
    public void PassFrequenciesWithBandRecovery_exchanges_vfos_when_initial_FA_writes_fail()
    {
        EnterSatelliteMode();
        RecordingTransport.FaHz = VhfUplinkHz;
        RecordingTransport.FbHz = UhfDownlinkHz;

        var rejectedFirstFa = false;
        RecordingTransport.ShouldRejectSet = cmd =>
        {
            if (rejectedFirstFa || !cmd.StartsWith("FA", StringComparison.OrdinalIgnoreCase))
                return false;

            rejectedFirstFa = true;
            return true;
        };

        var ok = Driver.ApplySatellitePassFrequenciesWithBandRecovery(
            UhfDownlinkHz,
            VhfUplinkHz,
            435_850.45,
            '2',
            '1');

        Assert.True(ok);
        Assert.Equal(UhfDownlinkHz, RecordingTransport.FaHz);
        Assert.Equal(VhfUplinkHz, RecordingTransport.FbHz);
    }

    [Fact]
    public void PassFrequenciesWithBandRecovery_reenters_sat_when_FA_writes_keep_failing()
    {
        EnterSatelliteMode();
        RecordingTransport.FaHz = VhfUplinkHz;
        RecordingTransport.FbHz = UhfDownlinkHz;
        RecordingTransport.ShouldRejectSet = cmd =>
            cmd.StartsWith("FA", StringComparison.OrdinalIgnoreCase);

        var ok = Driver.ApplySatellitePassFrequenciesWithBandRecovery(
            UhfDownlinkHz,
            VhfUplinkHz,
            435_850.45,
            '2',
            '1');

        Assert.False(ok);
        Assert.Contains("SA0010000;", GetSentCommands());
    }

    [Fact]
    public void Ts2000_pass_init_recovers_when_FA_rejected_on_inverted_band_layout()
    {
        var transport = new RecordingKenwoodCatTransport();
        var driver = new KenwoodTs2000Driver(transport, catDelayMs: 0, satModeSettlingDelayMs: 0, satModeRetryCount: 1, satModeRetryDelayMs: 0);
        var controller = new RigController(_ => driver);
        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.KenwoodTs2000,
            Port = "COM1",
            DopplerThresholdFmHz = 200,
            CatDelayMs = 0
        };

        var mode = new SatelliteTransponderMode
        {
            Type = "SSB Transponder",
            DownlinkKHz = 435_850.45,
            UplinkKHz = 145_952.65,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        var ctx = new RigTrackingContext
        {
            TrackState = new SatelliteTrackState
            {
                Name = "FO-29",
                NoradId = "24278",
                Subpoint = new GeoCoordinate(0, 0),
                LookAngles = new LookAngles(180, 45, 600, 2.0)
            },
            Mode = mode,
            Corrected = DopplerFrequencyCalculator.Compute(mode, 2.0, 0)
        };

        transport.FaHz = 145_952_000;
        transport.FbHz = 435_850_000;

        var rejectedFirstFa = false;
        transport.ShouldRejectSet = cmd =>
        {
            if (rejectedFirstFa || !cmd.StartsWith("FA", StringComparison.OrdinalIgnoreCase))
                return false;

            rejectedFirstFa = true;
            return true;
        };

        controller.Update(settings, ctx);
        controller.DrainCommandQueueForTests();

        var expectedRxHz = (long)Math.Round(ctx.Corrected.RadioReceiveKHz * 1000.0);
        var expectedTxHz = (long)Math.Round(ctx.Corrected.RadioTransmitKHz * 1000.0);
        Assert.Equal(expectedRxHz, transport.FaHz);
        Assert.Equal(expectedTxHz, transport.FbHz);
    }
}
