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
    public void Interactive_doppler_does_not_snap_rx_during_post_write_settle_when_operator_tunes()
    {
        var rig = new RecordingRigDriver();
        var controller = new RigController(_ => rig);
        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.IcomIc910,
            Port = "COM1",
            DopplerThresholdLinearHz = 50,
            DopplerAdaptiveThresholdEnabled = true,
            CatDelayMs = 0
        };

        var mode = new SatelliteTransponderMode
        {
            DownlinkKHz = 435_850.45,
            UplinkKHz = 145_952.65,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        RigTrackingContext Build(double rangeRateKmPerSec) => new()
        {
            TrackState = new SatelliteTrackState
            {
                Name = "FO-29",
                NoradId = "44208",
                Subpoint = new GeoCoordinate(0, 0),
                LookAngles = new LookAngles(180, 20, 800, rangeRateKmPerSec)
            },
            Mode = mode,
            Corrected = DopplerFrequencyCalculator.Compute(mode, rangeRateKmPerSec, 0),
            DopplerStrategy = DopplerStrategy.Full
        };

        controller.Update(settings, Build(0));
        Thread.Sleep(650);

        controller.PublishContext(settings, Build(4.2), reinitializePass: false);
        controller.RunTrackingLoopOnce();

        var operatorMainHz = rig.MainHz + 2_000;
        rig.MainHz = operatorMainHz;

        controller.RunTrackingLoopOnce();

        Assert.Equal(operatorMainHz, rig.MainHz);
    }

    [Fact]
    public void Interactive_doppler_still_tracks_during_post_write_settle_when_dial_matches_last_write()
    {
        var rig = new RecordingRigDriver();
        var controller = new RigController(_ => rig);
        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.IcomIc910,
            Port = "COM1",
            DopplerThresholdLinearHz = 50,
            DopplerAdaptiveThresholdEnabled = true,
            CatDelayMs = 0
        };

        var mode = new SatelliteTransponderMode
        {
            DownlinkKHz = 435_850.45,
            UplinkKHz = 145_952.65,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        RigTrackingContext Build(double rangeRateKmPerSec) => new()
        {
            TrackState = new SatelliteTrackState
            {
                Name = "FO-29",
                NoradId = "44208",
                Subpoint = new GeoCoordinate(0, 0),
                LookAngles = new LookAngles(180, 20, 800, rangeRateKmPerSec)
            },
            Mode = mode,
            Corrected = DopplerFrequencyCalculator.Compute(mode, rangeRateKmPerSec, 0),
            DopplerStrategy = DopplerStrategy.Full
        };

        controller.Update(settings, Build(0));
        Thread.Sleep(650);
        var mainAfterInit = rig.MainHz;

        controller.PublishContext(settings, Build(4.2), reinitializePass: false);
        controller.RunTrackingLoopOnce();
        Assert.NotEqual(mainAfterInit, rig.MainHz);

        var mainAfterFirstWrite = rig.MainHz;
        controller.PublishContext(settings, Build(5.2), reinitializePass: false);
        controller.RunTrackingLoopOnce();

        Assert.NotEqual(mainAfterFirstWrite, rig.MainHz);
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
