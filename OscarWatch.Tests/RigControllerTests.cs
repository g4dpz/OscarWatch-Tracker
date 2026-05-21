using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

public class RigControllerTests
{
    [Fact]
    public void Disabled_clears_connection_status()
    {
        var controller = new RigController();
        controller.Update(new RigSettings { Enabled = false }, null);
        var status = controller.GetStatus();
        Assert.False(status.IsTracking);
        Assert.False(status.IsConnected);
    }

    [Fact]
    public void Icom9700_uses_same_tracking_path_as_ic910()
    {
        var rig = new RecordingRigDriver();
        var controller = new RigController(_ => rig);
        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.IcomIc9700,
            Port = "COM1",
            DopplerThresholdFmHz = 200,
            CatDelayMs = 0,
            TrackStartElevationDeg = -90
        };

        var mode = new SatelliteTransponderMode
        {
            Type = "FM VOICE",
            DownlinkKHz = 436_795,
            UplinkKHz = 145_850,
            DownlinkMode = "FMN",
            UplinkMode = "FMN",
            CtcssHz = 67.0
        };

        var state = new SatelliteTrackState
        {
            Name = "SO-50",
            NoradId = "25544",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(180, 20, 800, 2.5)
        };

        var ctx = new RigTrackingContext
        {
            TrackState = state,
            Mode = mode,
            Corrected = new CorrectedFrequencies(145852, 145952, 145850, 145950, 2, false),
            SelectedCtcssHz = 67.0
        };

        controller.Update(settings, ctx);
        var status = controller.GetStatus();
        Assert.True(status.IsConnected);
        Assert.True(status.IsTracking);
        Assert.DoesNotContain("not yet implemented", status.StatusMessage ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(status.LastReceiveHz);
        Assert.NotNull(status.LastTransmitHz);
        Assert.Equal(67.0, rig.LastToneHz);
        Assert.Equal(RigVfo.Sub, rig.LastToneVfo);
    }

    [Fact]
    public void Dummy_tracking_sets_receive_frequency()
    {
        var controller = new RigController();
        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.Dummy,
            DopplerThresholdFmHz = 200,
            DopplerThresholdLinearHz = 50,
            CatDelayMs = 0,
            TrackStartElevationDeg = -90
        };

        var mode = new SatelliteTransponderMode
        {
            Type = "FM",
            DownlinkKHz = 145950,
            UplinkKHz = 145850,
            DownlinkMode = "FMN",
            UplinkMode = "FMN",
            Doppler = "NOR"
        };

        var state = new SatelliteTrackState
        {
            Name = "SO-50",
            NoradId = "25544",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(180, 20, 800, 2.5)
        };

        var ctx = new RigTrackingContext
        {
            TrackState = state,
            Mode = mode,
            Corrected = new CorrectedFrequencies(145852, 145952, 145850, 145950, 2, false),
            TransmitOffsetKHz = 0,
            ReceiveOffsetKHz = 0
        };

        controller.Update(settings, ctx);
        var status = controller.GetStatus();
        Assert.True(status.IsConnected);
        Assert.True(status.IsTracking);
        Assert.NotNull(status.LastReceiveHz);
    }

    [Fact]
    public void Cat_paused_skips_tracking()
    {
        var controller = new RigController();
        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.Dummy,
            CatUpdatesPaused = true,
            TrackStartElevationDeg = -90
        };

        var mode = new SatelliteTransponderMode
        {
            DownlinkKHz = 145950,
            UplinkKHz = 145850,
            DownlinkMode = "FMN",
            UplinkMode = "FMN"
        };

        var state = new SatelliteTrackState
        {
            Name = "SO-50",
            NoradId = "25544",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(180, 20, 800, 2.5)
        };

        var ctx = new RigTrackingContext
        {
            TrackState = state,
            Mode = mode,
            Corrected = new CorrectedFrequencies(145850, 145950, 145850, 145950, 0, false),
            TransmitOffsetKHz = 0,
            ReceiveOffsetKHz = 0
        };

        controller.Update(settings, ctx);
        var status = controller.GetStatus();
        Assert.True(status.CatUpdatesPaused);
        Assert.False(status.IsTracking);
        Assert.Contains("paused", status.StatusMessage ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(status.LastReceiveHz);
        Assert.NotNull(status.LastTransmitHz);
    }

    [Fact]
    public void Below_track_elevation_does_not_track()
    {
        var controller = new RigController();
        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.Dummy,
            TrackStartElevationDeg = 5
        };

        var mode = new SatelliteTransponderMode
        {
            DownlinkKHz = 145950,
            UplinkKHz = 145850,
            DownlinkMode = "FMN",
            UplinkMode = "FMN"
        };

        var state = new SatelliteTrackState
        {
            Name = "SO-50",
            NoradId = "25544",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(180, 1, 800, 0)
        };

        var ctx = new RigTrackingContext
        {
            TrackState = state,
            Mode = mode,
            Corrected = new CorrectedFrequencies(145850, 145950, 145850, 145950, 0, false),
            TransmitOffsetKHz = 0,
            ReceiveOffsetKHz = 0
        };

        controller.Update(settings, ctx);
        var status = controller.GetStatus();
        Assert.False(status.IsTracking);
        Assert.NotNull(status.LastReceiveHz);
        Assert.NotNull(status.LastTransmitHz);
    }

    [Fact]
    public void Tx_only_offset_triggers_cat_update_on_uv_satellite()
    {
        var controller = new RigController();
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
            UplinkKHz = 145_937,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        var state = new SatelliteTrackState
        {
            Name = "RS-44",
            NoradId = "99999",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(180, 20, 800, 0)
        };

        RigTrackingContext Build(double txOffset, double rxOffset)
        {
            var corrected = DopplerFrequencyCalculator.Compute(mode, 0, txOffset, rxOffset);
            return new RigTrackingContext
            {
                TrackState = state,
                Mode = mode,
                Corrected = corrected,
                TransmitOffsetKHz = txOffset,
                ReceiveOffsetKHz = rxOffset
            };
        }

        controller.Update(settings, Build(0, 0));
        var before = controller.GetStatus().LastTransmitHz;

        controller.Update(settings, Build(2.0, 0));
        var after = controller.GetStatus().LastTransmitHz;

        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.True(after < before);
    }

    [Fact]
    public void Rx_offset_sends_cat_frequency_to_rig()
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
            DownlinkKHz = 435_659.9,
            UplinkKHz = 145_960,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "NOR"
        };

        var state = new SatelliteTrackState
        {
            Name = "RS-44",
            NoradId = "99999",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(180, 20, 800, 0)
        };

        RigTrackingContext Build(double rxOffset) =>
            new()
            {
                TrackState = state,
                Mode = mode,
                Corrected = DopplerFrequencyCalculator.Compute(mode, 0, 0, rxOffset),
                TransmitOffsetKHz = 0,
                ReceiveOffsetKHz = rxOffset
            };

        controller.Update(settings, Build(0));
        var baselineMain = rig.MainHz;
        Assert.True(baselineMain > 0);

        var callsAfterInit = rig.SetFrequencyCallCount;
        controller.Update(settings, Build(5.524));
        Assert.True(rig.SetFrequencyCallCount > callsAfterInit);
        Assert.Equal(baselineMain + 5_524, rig.MainHz);
    }

    [Fact]
    public void Ctcss_tone_change_commands_uplink_sub_vfo()
    {
        var rig = new RecordingRigDriver();
        var controller = new RigController(_ => rig);
        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.Dummy,
            Region = RigRegion.USA,
            CatDelayMs = 0,
            TrackStartElevationDeg = -90
        };

        var mode = new SatelliteTransponderMode
        {
            Type = "FM VOICE",
            DownlinkKHz = 436_795,
            UplinkKHz = 145_850,
            DownlinkMode = "FMN",
            UplinkMode = "FMN",
            CtcssHz = 67.0,
            CtcssArmHz = 74.4
        };

        var state = new SatelliteTrackState
        {
            Name = "SO-50",
            NoradId = "25544",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(180, 20, 800, 0)
        };

        RigTrackingContext Build(double toneHz) => new()
        {
            TrackState = state,
            Mode = mode,
            Corrected = DopplerFrequencyCalculator.Compute(mode, 0, 0, 0),
            SelectedCtcssHz = toneHz
        };

        controller.Update(settings, Build(67.0));
        Assert.Equal(67.0, rig.LastToneHz);
        Assert.True(rig.LastToneSquelch);
        Assert.True(rig.ToneSquelchOn);
        Assert.Equal(RigVfo.Sub, rig.LastToneVfo);

        controller.Update(settings, Build(74.4));
        Assert.Equal(74.4, rig.LastToneHz);
        Assert.True(rig.ToneSquelchOn);
        Assert.Equal(RigVfo.Sub, rig.LastToneVfo);
    }

    [Fact]
    public void ApplySelectedCtcss_from_selector_while_cat_paused()
    {
        var rig = new RecordingRigDriver();
        var controller = new RigController(_ => rig);
        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.IcomIc910,
            Port = "COM1",
            Region = RigRegion.USA,
            CatDelayMs = 0,
            TrackStartElevationDeg = 10
        };

        var mode = new SatelliteTransponderMode
        {
            Type = "FM VOICE",
            DownlinkKHz = 436_795,
            UplinkKHz = 145_850,
            DownlinkMode = "FMN",
            UplinkMode = "FMN",
            CtcssHz = 67.0,
            CtcssArmHz = 74.4
        };

        var state = new SatelliteTrackState
        {
            Name = "SO-50",
            NoradId = "25544",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(180, 20, 800, 0)
        };

        var access = new RigTrackingContext
        {
            TrackState = state,
            Mode = mode,
            Corrected = DopplerFrequencyCalculator.Compute(mode, 0, 0, 0),
            SelectedCtcssHz = 67.0
        };

        controller.Update(settings, access);
        Assert.Equal(67.0, rig.LastToneHz);

        settings.CatUpdatesPaused = true;
        var arm = new RigTrackingContext
        {
            TrackState = state,
            Mode = mode,
            Corrected = access.Corrected,
            SelectedCtcssHz = 74.4
        };
        controller.PublishContext(settings, arm);
        Assert.Equal(67.0, rig.LastToneHz);

        controller.ApplySelectedCtcss(settings, arm);
        Assert.Equal(74.4, rig.LastToneHz);
        Assert.Equal(RigVfo.Sub, rig.LastToneVfo);
        Assert.True(rig.ToneSquelchOn);
    }

    [Fact]
    public void Icom_ic910_ctcss_always_selects_sub_before_tone_commands()
    {
        var rig = new RecordingRigDriver();
        var controller = new RigController(_ => rig);
        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.IcomIc910,
            Port = "COM1",
            CatDelayMs = 0,
            TrackStartElevationDeg = -90
        };

        var mode = new SatelliteTransponderMode
        {
            Type = "FM VOICE",
            DownlinkKHz = 436_795,
            UplinkKHz = 145_850,
            DownlinkMode = "FMN",
            UplinkMode = "FMN",
            CtcssHz = 67.0
        };

        var state = new SatelliteTrackState
        {
            Name = "SO-50",
            NoradId = "25544",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(180, 20, 800, 0)
        };

        var ctx = new RigTrackingContext
        {
            TrackState = state,
            Mode = mode,
            Corrected = DopplerFrequencyCalculator.Compute(mode, 0, 0, 0),
            SelectedCtcssHz = 67.0
        };

        controller.Update(settings, ctx);
        Assert.Equal(RigVfo.Sub, rig.LastToneVfo);
        Assert.Equal(67.0, rig.LastToneHz);
    }

    [Fact]
    public void Spinner_offset_survives_small_main_vfo_read_jitter_without_manual_rx_drift()
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
            DownlinkKHz = 435_850.45,
            UplinkKHz = 145_952.65,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        var state = new SatelliteTrackState
        {
            Name = "FO-29",
            NoradId = "44208",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(180, 20, 800, 0)
        };

        RigTrackingContext Build(double txOffset) =>
            new()
            {
                TrackState = state,
                Mode = mode,
                Corrected = DopplerFrequencyCalculator.Compute(mode, 0, txOffset, 0),
                TransmitOffsetKHz = txOffset,
                ReceiveOffsetKHz = 0
            };

        controller.Update(settings, Build(0));
        Thread.Sleep(650);
        controller.Update(settings, Build(9.8));
        var rxAfterOffset = rig.MainHz;

        rig.MainHz = rxAfterOffset + 35;
        for (var i = 0; i < 8; i++)
            controller.RunTrackingLoopOnce();

        var status = controller.GetStatus();
        Assert.InRange(status.ManualReceiveAdjustKHz, -0.001, 0.001);
        Assert.InRange(status.ManualTransmitAdjustKHz, -0.001, 0.001);
    }

    [Fact]
    public void Rev_linear_knob_tune_keeps_receive_and_moves_transmit_opposite()
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
            LookAngles = new LookAngles(180, 20, 800, 0)
        };

        var ctx = new RigTrackingContext
        {
            TrackState = state,
            Mode = mode,
            Corrected = DopplerFrequencyCalculator.Compute(mode, 0, 0, 0),
            TransmitOffsetKHz = 0,
            ReceiveOffsetKHz = 0
        };

        controller.Update(settings, ctx);
        Thread.Sleep(650);
        var rxAfterInit = rig.MainHz;
        var txAfterInit = rig.SubHz;
        Assert.True(rxAfterInit > 0);
        Assert.True(txAfterInit > 0);

        rig.MainHz = rxAfterInit + 2_500;
        for (var i = 0; i < 8; i++)
            controller.RunTrackingLoopOnce();

        Assert.Equal(rxAfterInit + 2_500, rig.MainHz);
        Assert.True(rig.SubHz < txAfterInit, $"REV expects TX to drop when RX rises: tx={rig.SubHz} was {txAfterInit}");
        var status = controller.GetStatus();
        Assert.InRange(status.ManualReceiveAdjustKHz, 2.4, 2.6);
        Assert.InRange(status.ManualTransmitAdjustKHz, -2.6, -2.4);
    }

    [Fact]
    public void Nor_linear_knob_tune_moves_both_legs_same_direction()
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
            UplinkKHz = 145_960,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "NOR"
        };

        var state = new SatelliteTrackState
        {
            Name = "TEST",
            NoradId = "1",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(180, 20, 800, 0)
        };

        var ctx = new RigTrackingContext
        {
            TrackState = state,
            Mode = mode,
            Corrected = DopplerFrequencyCalculator.Compute(mode, 0, 0, 0),
            TransmitOffsetKHz = 0,
            ReceiveOffsetKHz = 0
        };

        controller.Update(settings, ctx);
        Thread.Sleep(650);
        var rxAfterInit = rig.MainHz;
        var txAfterInit = rig.SubHz;

        rig.MainHz = rxAfterInit + 3_000;
        for (var i = 0; i < 8; i++)
            controller.RunTrackingLoopOnce();

        Assert.Equal(rxAfterInit + 3_000, rig.MainHz);
        Assert.True(rig.SubHz > txAfterInit);
        var status = controller.GetStatus();
        Assert.InRange(status.ManualReceiveAdjustKHz, 2.9, 3.1);
        Assert.InRange(status.ManualTransmitAdjustKHz, 2.9, 3.1);
    }

    [Fact]
    public void Single_ctcss_mode_applies_access_tone_without_combo_selection()
    {
        var rig = new RecordingRigDriver();
        var controller = new RigController(_ => rig);
        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.Dummy,
            Region = RigRegion.EU,
            CatDelayMs = 0,
            TrackStartElevationDeg = -90
        };

        var mode = new SatelliteTransponderMode
        {
            Type = "FM",
            DownlinkKHz = 145_825,
            UplinkKHz = 145_825,
            DownlinkMode = "FMN",
            UplinkMode = "FMN",
            CtcssHz = 67.0
        };

        var state = new SatelliteTrackState
        {
            Name = "AO-91",
            NoradId = "1",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(180, 20, 800, 0)
        };

        var ctx = new RigTrackingContext
        {
            TrackState = state,
            Mode = mode,
            Corrected = DopplerFrequencyCalculator.Compute(mode, 0, 0, 0),
            SelectedCtcssHz = 67.0
        };

        controller.Update(settings, ctx);
        Assert.Equal(67.0, rig.LastToneHz);
        Assert.False(rig.LastToneSquelch);
        Assert.True(rig.ToneOn);
    }

    [Fact]
    public void Fm_cross_band_updates_tx_when_rx_threshold_hit_first()
    {
        var rig = new RecordingRigDriver();
        var controller = new RigController(_ => rig);
        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.Dummy,
            DopplerThresholdFmHz = 200,
            CatDelayMs = 0,
            TrackStartElevationDeg = -90
        };

        var mode = new SatelliteTransponderMode
        {
            DownlinkKHz = 437_800,
            UplinkKHz = 145_990,
            DownlinkMode = "FM",
            UplinkMode = "FM",
            Doppler = "NOR"
        };

        RigTrackingContext Build(double rangeRateKmPerSec) =>
            new()
            {
                TrackState = new SatelliteTrackState
                {
                    Name = "ISS",
                    NoradId = "25544",
                    Subpoint = new GeoCoordinate(0, 0),
                    LookAngles = new LookAngles(180, 30, 400, rangeRateKmPerSec)
                },
                Mode = mode,
                Corrected = DopplerFrequencyCalculator.Compute(mode, rangeRateKmPerSec, 0, 0),
                TransmitOffsetKHz = 0,
                ReceiveOffsetKHz = 0
            };

        controller.Update(settings, Build(0));
        var txAtRest = rig.SubHz;
        Assert.True(txAtRest > 0);

        controller.Update(settings, Build(0.25));
        Assert.NotEqual(txAtRest, rig.SubHz);
    }
}
