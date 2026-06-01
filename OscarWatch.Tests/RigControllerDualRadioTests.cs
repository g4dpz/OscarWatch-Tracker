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
}
