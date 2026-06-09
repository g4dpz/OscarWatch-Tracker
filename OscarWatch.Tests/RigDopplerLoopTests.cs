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
            CatDelayMs = 0
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
            Corrected = DopplerFrequencyCalculator.Compute(mode, 2.5, 0),
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
        Thread.Sleep(2600);
        for (var i = 0; i < 8; i++)
            controller.RunTrackingLoopOnce();

        Assert.Equal(baselineRx + 1_000, rig.MainHz);
        Assert.NotEqual(baselineTx, rig.SubHz);
    }

    [Fact]
    public void Automatic_fm_skips_vfo_select_when_doppler_is_below_threshold()
    {
        var rig = new RecordingRigDriver();
        var controller = new RigController(_ => rig);
        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.IcomIc9700,
            Port = "COM1",
            DopplerThresholdFmHz = 350,
            CatDelayMs = 0
        };

        var mode = new SatelliteTransponderMode
        {
            Type = "Cross band repeater",
            DownlinkKHz = 437_800,
            UplinkKHz = 145_990,
            DownlinkMode = "FM",
            UplinkMode = "FM",
            Doppler = "NOR"
        };

        var ctx = new RigTrackingContext
        {
            TrackState = new SatelliteTrackState
            {
                Name = "ISS",
                NoradId = "25544",
                Subpoint = new GeoCoordinate(0, 0),
                LookAngles = new LookAngles(180, 20, 800, 0.05)
            },
            Mode = mode,
            Corrected = DopplerFrequencyCalculator.Compute(mode, 0.05, 0)
        };

        controller.Update(settings, ctx);
        controller.DrainCommandQueueForTests();

        var vfoCallsAfterInit = rig.SelectVfoCallCount;
        var freqCallsAfterInit = rig.SetFrequencyCallCount;

        for (var i = 0; i < 5; i++)
            controller.RunTrackingLoopOnce();

        Assert.Equal(vfoCallsAfterInit, rig.SelectVfoCallCount);
        Assert.Equal(freqCallsAfterInit, rig.SetFrequencyCallCount);
    }
}
