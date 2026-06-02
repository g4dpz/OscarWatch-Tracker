using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

public sealed class RigControllerDualRadioTests
{
    [Fact]
    public void Dual_pass_init_writes_rx_on_downlink_and_tx_on_uplink()
    {
        var downTransport = new RecordingYaesuCatTransport();
        var upTransport = new RecordingYaesuCatTransport();
        var controller = new RigController(
            endpointFactory: ep => ep.Port == "COM_DL"
                ? new YaesuFt817Driver(RigType.YaesuFt817, downTransport)
                : new YaesuFt818Driver(upTransport));

        var settings = new RigSettings
        {
            Enabled = true,
            DualRadioEnabled = true,
            Downlink = new RigEndpointSettings
            {
                Type = RigType.YaesuFt817,
                Port = "COM_DL",
                BaudRate = 38400,
                CatDelayMs = 0
            },
            Uplink = new RigEndpointSettings
            {
                Type = RigType.YaesuFt818,
                Port = "COM_UL",
                BaudRate = 38400,
                CatDelayMs = 0,
                Region = RigRegion.USA
            }
        };

        var mode = new SatelliteTransponderMode
        {
            Type = "Voice U/V",
            DownlinkKHz = 145_960,
            UplinkKHz = 435_250,
            DownlinkMode = "FM",
            UplinkMode = "FM",
            Doppler = "NOR",
            CtcssHz = 67.0
        };

        controller.Update(settings, new RigTrackingContext
        {
            TrackState = new SatelliteTrackState
            {
                Name = "AO-91",
                NoradId = "43017",
                Subpoint = new GeoCoordinate(0, 0),
                LookAngles = new LookAngles(180, 30, 800, 0)
            },
            Mode = mode,
            Corrected = DopplerFrequencyCalculator.Compute(mode, 0, 0),
            SelectedCtcssHz = 67.0
        });

        Assert.DoesNotContain(downTransport.SentFrames, f => f.SequenceEqual(YaesuFt817CatCodec.SplitOn.ToArray()));
        Assert.Contains(downTransport.SentFrames, f => f.Length == 5 && f[4] == 0x01);
        Assert.Contains(upTransport.SentFrames, f => f.Length == 5 && f[4] == 0x01);
        Assert.Contains(upTransport.SentFrames, f => f.Length == 5 && f[4] == 0x0b);
    }

    [Fact]
    public void Dual_linear_full_doppler_updates_both_radios_when_range_rate_changes()
    {
        var downRig = new RecordingRigDriver();
        var upRig = new RecordingRigDriver();
        var controller = new RigController(
            endpointFactory: ep => ep.Port == "COM_DL" ? downRig : upRig);

        var settings = new RigSettings
        {
            Enabled = true,
            DualRadioEnabled = true,
            DopplerThresholdLinearHz = 50,
            Downlink = new RigEndpointSettings
            {
                Type = RigType.YaesuFt817,
                Port = "COM_DL",
                BaudRate = 38400,
                CatDelayMs = 0
            },
            Uplink = new RigEndpointSettings
            {
                Type = RigType.YaesuFt818,
                Port = "COM_UL",
                BaudRate = 38400,
                CatDelayMs = 0
            }
        };

        var mode = new SatelliteTransponderMode
        {
            Type = "Voice U/V",
            DownlinkKHz = 145_960,
            UplinkKHz = 435_250,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "NOR"
        };

        RigTrackingContext Build(double rangeRateKmPerSec) => new()
        {
            TrackState = new SatelliteTrackState
            {
                Name = "RS-44",
                NoradId = "44909",
                Subpoint = new GeoCoordinate(0, 0),
                LookAngles = new LookAngles(180, 30, 800, rangeRateKmPerSec)
            },
            Mode = mode,
            Corrected = DopplerFrequencyCalculator.Compute(mode, rangeRateKmPerSec, 0),
            DopplerStrategy = DopplerStrategy.Full
        };

        controller.Update(settings, Build(0));
        Thread.Sleep(650);

        var rxAtRest = downRig.MainHz;
        var txAtRest = upRig.MainHz;
        Assert.True(rxAtRest > 0);
        Assert.True(txAtRest > 0);

        controller.PublishContext(settings, Build(4.2));
        for (var i = 0; i < 8; i++)
            controller.RunTrackingLoopOnce();

        var expectedRx = (long)(DopplerFrequencyCalculator.Compute(mode, 4.2, 0).RadioReceiveKHz * 1000);
        var expectedTx = (long)(DopplerFrequencyCalculator.Compute(mode, 4.2, 0).RadioTransmitKHz * 1000);

        Assert.InRange(downRig.MainHz, expectedRx - 55, expectedRx + 55);
        Assert.InRange(upRig.MainHz, expectedTx - 55, expectedTx + 55);
        Assert.NotEqual(rxAtRest, downRig.MainHz);
        Assert.NotEqual(txAtRest, upRig.MainHz);
    }

    [Fact]
    public void Dual_linear_holds_downlink_cat_while_operator_spins_but_tracks_uplink()
    {
        var downRig = new RecordingRigDriver();
        var upRig = new RecordingRigDriver();
        var controller = new RigController(
            endpointFactory: ep => ep.Port == "COM_DL" ? downRig : upRig);

        var settings = new RigSettings
        {
            Enabled = true,
            DualRadioEnabled = true,
            DopplerThresholdLinearHz = 50,
            Downlink = new RigEndpointSettings { Type = RigType.YaesuFt817, Port = "COM_DL", CatDelayMs = 0 },
            Uplink = new RigEndpointSettings { Type = RigType.YaesuFt818, Port = "COM_UL", CatDelayMs = 0 }
        };

        var mode = new SatelliteTransponderMode
        {
            DownlinkKHz = 145_960,
            UplinkKHz = 435_250,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "NOR"
        };

        RigTrackingContext Build(double rangeRateKmPerSec) => new()
        {
            TrackState = new SatelliteTrackState
            {
                Name = "RS-44",
                NoradId = "44909",
                Subpoint = new GeoCoordinate(0, 0),
                LookAngles = new LookAngles(180, 30, 800, rangeRateKmPerSec)
            },
            Mode = mode,
            Corrected = DopplerFrequencyCalculator.Compute(mode, rangeRateKmPerSec, 0),
            DopplerStrategy = DopplerStrategy.Full
        };

        controller.Update(settings, Build(0));
        Thread.Sleep(650);

        var rxAfterInit = downRig.MainHz;
        var txAfterInit = upRig.MainHz;

        controller.PublishContext(settings, Build(4.2));
        for (var i = 0; i < 14; i++)
        {
            downRig.MainHz = rxAfterInit + 1_500 + i * 200;
            controller.RunTrackingLoopOnce();
        }

        Assert.Equal(rxAfterInit + 1_500 + 13 * 200, downRig.MainHz);
        Assert.NotEqual(txAfterInit, upRig.MainHz);
    }

    [Fact]
    public void Dual_linear_manual_downlink_offset_does_not_shift_uplink_passband()
    {
        var downRig = new RecordingRigDriver();
        var upRig = new RecordingRigDriver();
        var controller = new RigController(
            endpointFactory: ep => ep.Port == "COM_DL" ? downRig : upRig);

        var settings = new RigSettings
        {
            Enabled = true,
            DualRadioEnabled = true,
            DopplerThresholdLinearHz = 50,
            Downlink = new RigEndpointSettings { Type = RigType.YaesuFt817, Port = "COM_DL", CatDelayMs = 0 },
            Uplink = new RigEndpointSettings { Type = RigType.YaesuFt818, Port = "COM_UL", CatDelayMs = 0 }
        };

        var mode = new SatelliteTransponderMode
        {
            DownlinkKHz = 145_960,
            UplinkKHz = 435_250,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        RigTrackingContext Build(double rangeRateKmPerSec) => new()
        {
            TrackState = new SatelliteTrackState
            {
                Name = "RS-44",
                NoradId = "44909",
                Subpoint = new GeoCoordinate(0, 0),
                LookAngles = new LookAngles(180, 30, 800, rangeRateKmPerSec)
            },
            Mode = mode,
            Corrected = DopplerFrequencyCalculator.Compute(mode, rangeRateKmPerSec, 0),
            DopplerStrategy = DopplerStrategy.Full
        };

        controller.Update(settings, Build(0));
        Thread.Sleep(650);

        downRig.MainHz += 2_000;
        for (var i = 0; i < 10; i++)
            controller.RunTrackingLoopOnce();

        var status = controller.GetStatus();
        Assert.InRange(status.ManualReceiveAdjustKHz, 1.9, 2.1);
        Assert.InRange(status.ManualTransmitAdjustKHz, -0.001, 0.001);
    }

    [Fact]
    public void Mixed_ic705_downlink_and_ft818_uplink_pass_init_writes_both_legs()
    {
        var downTransport = new RecordingIcomCivTransport();
        var upTransport = new RecordingYaesuCatTransport();
        var controller = new RigController(
            endpointFactory: ep => ep.Port == "COM_DL"
                ? new IcomIc705Driver(downTransport)
                : new YaesuFt818Driver(upTransport));

        var settings = new RigSettings
        {
            Enabled = true,
            DualRadioEnabled = true,
            Downlink = new RigEndpointSettings
            {
                Type = RigType.IcomIc705,
                Port = "COM_DL",
                BaudRate = 115200,
                CivAddress = "A4",
                CatDelayMs = 0
            },
            Uplink = new RigEndpointSettings
            {
                Type = RigType.YaesuFt818,
                Port = "COM_UL",
                BaudRate = 4800,
                CatDelayMs = 0,
                Region = RigRegion.EU
            }
        };

        var mode = new SatelliteTransponderMode
        {
            Type = "Voice U/V",
            DownlinkKHz = 145_960,
            UplinkKHz = 435_250,
            DownlinkMode = "FM",
            UplinkMode = "FM",
            Doppler = "NOR",
            CtcssHz = 67.0
        };

        controller.Update(settings, new RigTrackingContext
        {
            TrackState = new SatelliteTrackState
            {
                Name = "AO-91",
                NoradId = "43017",
                Subpoint = new GeoCoordinate(0, 0),
                LookAngles = new LookAngles(180, 30, 800, 0)
            },
            Mode = mode,
            Corrected = DopplerFrequencyCalculator.Compute(mode, 0, 0),
            SelectedCtcssHz = 67.0
        });

        Assert.Contains(downTransport.SentCommandBodies, b => b == "0700");
        Assert.Contains(upTransport.SentFrames, f => f.Length == 5 && f[4] == 0x01);
    }

    [Fact]
    public void Mixed_ft991_downlink_and_ft818_uplink_pass_init_writes_both_legs()
    {
        var downTransport = new RecordingYaesuNewCatTransport();
        var upTransport = new RecordingYaesuCatTransport();
        var controller = new RigController(
            endpointFactory: ep => ep.Port == "COM_DL"
                ? new YaesuFt991Driver(RigType.YaesuFt991, downTransport)
                : new YaesuFt818Driver(upTransport));

        var settings = new RigSettings
        {
            Enabled = true,
            DualRadioEnabled = true,
            Downlink = new RigEndpointSettings
            {
                Type = RigType.YaesuFt991,
                Port = "COM_DL",
                BaudRate = 38400,
                CatDelayMs = 0
            },
            Uplink = new RigEndpointSettings
            {
                Type = RigType.YaesuFt818,
                Port = "COM_UL",
                BaudRate = 4800,
                CatDelayMs = 0,
                Region = RigRegion.EU
            }
        };

        var mode = new SatelliteTransponderMode
        {
            Type = "Voice U/V",
            DownlinkKHz = 145_960,
            UplinkKHz = 435_250,
            DownlinkMode = "FM",
            UplinkMode = "FM",
            Doppler = "NOR",
            CtcssHz = 67.0
        };

        controller.Update(settings, new RigTrackingContext
        {
            TrackState = new SatelliteTrackState
            {
                Name = "AO-91",
                NoradId = "43017",
                Subpoint = new GeoCoordinate(0, 0),
                LookAngles = new LookAngles(180, 30, 800, 0)
            },
            Mode = mode,
            Corrected = DopplerFrequencyCalculator.Compute(mode, 0, 0),
            SelectedCtcssHz = 67.0
        });

        Assert.Contains(downTransport.SentCommands, c => c.StartsWith("FA", StringComparison.Ordinal));
        Assert.Contains(upTransport.SentFrames, f => f.Length == 5 && f[4] == 0x01);
    }
}
