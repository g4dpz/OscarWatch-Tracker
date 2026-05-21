using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

public class RigDopplerLoopTests
{
    [Fact]
    public void Interactive_doppler_skips_cat_while_dial_is_moving()
    {
        var rig = new RecordingRigDriver();
        var controller = new RigController(_ => rig);
        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.Dummy,
            DopplerThresholdLinearHz = 50,
            CatDelayMs = 0,
            TrackStartElevationDeg = -90
        };

        var mode = new SatelliteTransponderMode
        {
            DownlinkKHz = 435_667,
            UplinkKHz = 145_937.61,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        var state = new SatelliteTrackState
        {
            Name = "RS-44",
            NoradId = "99999",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(180, 20, 800, 2.5)
        };

        var ctx = new RigTrackingContext
        {
            TrackState = state,
            Mode = mode,
            Corrected = DopplerFrequencyCalculator.Compute(mode, 2.5, 0, 0),
            TransmitOffsetKHz = 0,
            ReceiveOffsetKHz = 0
        };

        controller.Update(settings, ctx);
        Thread.Sleep(650);
        var baselineRx = rig.MainHz;
        var baselineTx = rig.SubHz;

        rig.MainHz = baselineRx + 1_000;
        controller.RunTrackingLoopOnce();
        Assert.Equal(baselineTx, rig.SubHz);

        rig.MainHz = baselineRx + 1_000;
        for (var i = 0; i < 8; i++)
            controller.RunTrackingLoopOnce();

        Assert.Equal(baselineRx + 1_000, rig.MainHz);
        Assert.NotEqual(baselineTx, rig.SubHz);
    }
}
