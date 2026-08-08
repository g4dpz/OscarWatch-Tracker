using OscarWatch.Core.Models;
using OscarWatch.Rotator;

namespace OscarWatch.Tests;

public sealed class RotatorControllerTests
{
    [Fact]
    public void Update_runs_on_worker_thread_and_tracks_satellite()
    {
        var rotator = new RecordingRotatorDriver();
        using var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            BaudRate = 9600,
            Type = RotatorType.YaesuGs232,
            TrackStartElevationDeg = 5
        };

        var target = new SatelliteTrackState
        {
            Name = "ISS",
            NoradId = "25544",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(45, 20, 800, 0)
        };

        controller.UpdateSynchronously(settings, target);

        Assert.Equal(1, rotator.SetPositionCallCount);
        Assert.Equal(45, rotator.LastAzimuthDeg);
        Assert.Equal(20, rotator.LastElevationDeg);
        Assert.False(controller.GetPositionStatus().IsParked);

        var status = controller.GetPositionStatus();
        Assert.True(status.IsConnected);
        Assert.Equal(45, status.AzimuthDeg);
        Assert.Equal(20, status.ElevationDeg);
    }

    [Fact]
    public void Update_parks_when_satellite_drops_below_track_start()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            TrackStartElevationDeg = 5,
            ParkAzimuthDeg = 180,
            ParkElevationDeg = 0,
            ParkAfterPass = true
        };

        controller.UpdateSynchronously(settings, TrackTarget("25544", 45, 3));

        Assert.Equal(180, rotator.LastAzimuthDeg);
        Assert.Equal(0, rotator.LastElevationDeg);
        Assert.True(controller.GetPositionStatus().IsParked);
    }

    [Fact]
    public void Update_skips_automatic_park_after_pass_when_disabled()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            TrackStartElevationDeg = 5,
            ParkAzimuthDeg = 180,
            ParkElevationDeg = 0,
            ParkAfterPass = false
        };

        controller.UpdateSynchronously(settings, TrackTarget("25544", 45, 3));

        Assert.Equal(0, rotator.SetPositionCallCount);
        Assert.False(controller.GetPositionStatus().IsParked);
    }

    [Fact]
    public void DisconnectAndWait_releases_driver_and_does_not_reconnect()
    {
        var rotator = new RecordingRotatorDriver();
        var createCount = 0;
        var controller = new RotatorController(_ =>
        {
            createCount++;
            return rotator;
        });
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            BaudRate = 9600,
            Type = RotatorType.YaesuGs232,
            TrackStartElevationDeg = 5
        };

        controller.UpdateSynchronously(settings, TrackTarget("25544", 45, 20));
        Assert.Equal(1, createCount);
        Assert.Equal(1, rotator.OpenCallCount);
        Assert.True(controller.GetPositionStatus().IsConnected);

        // DisconnectAndWait completes after TearDown; the same loop iteration then runs
        // RunTrackingIteration — which must not reopen when cached settings were cleared.
        controller.DisconnectAndWait();

        Assert.Equal(1, createCount);
        Assert.Equal(1, rotator.OpenCallCount);
        Assert.Equal(1, rotator.DisposeCallCount);
        Assert.False(controller.GetPositionStatus().IsConnected);

        controller.Dispose();
    }

    [Fact]
    public void Manual_park_still_works_when_park_after_pass_disabled()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            ParkAzimuthDeg = 180,
            ParkElevationDeg = 0,
            ParkAfterPass = false
        };

        controller.Park(settings);
        controller.DrainCommandQueueForTests();

        Assert.Equal(180, rotator.LastAzimuthDeg);
        Assert.True(controller.GetPositionStatus().IsParked);
    }

    [Fact]
    public void Park_command_sends_park_position()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            ParkAzimuthDeg = 180,
            ParkElevationDeg = 0
        };

        controller.Park(settings);
        controller.DrainCommandQueueForTests();

        Assert.Equal(180, rotator.LastAzimuthDeg);
        Assert.Equal(0, rotator.LastElevationDeg);
        Assert.True(controller.GetPositionStatus().IsParked);
    }

    [Fact]
    public void Tracking_applies_azimuth_and_elevation_calibration_offsets()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            TrackStartElevationDeg = 5,
            AzimuthOffsetDeg = 2.5,
            ElevationOffsetDeg = -1.0
        };

        var target = new SatelliteTrackState
        {
            Name = "ISS",
            NoradId = "25544",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(45, 20, 800, 0)
        };

        controller.UpdateSynchronously(settings, target);

        Assert.Equal(47.5, rotator.LastAzimuthDeg);
        Assert.Equal(19, rotator.LastElevationDeg);
    }

    [Fact]
    public void Park_ignores_calibration_offsets()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            ParkAzimuthDeg = 180,
            ParkElevationDeg = 10,
            AzimuthOffsetDeg = -72,
            ElevationOffsetDeg = 2
        };

        controller.Park(settings);
        controller.DrainCommandQueueForTests();

        Assert.Equal(180, rotator.LastAzimuthDeg);
        Assert.Equal(10, rotator.LastElevationDeg);
    }

    [Fact]
    public void Standby_does_not_park_when_park_after_pass_disabled()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            ParkAzimuthDeg = 180,
            ParkElevationDeg = 0,
            ParkAfterPass = false
        };

        controller.SetStandby(true, settings);
        controller.DrainCommandQueueForTests();

        Assert.Equal(0, rotator.SetPositionCallCount);
        Assert.False(controller.GetPositionStatus().IsParked);

        controller.UpdateSynchronously(settings, null);
        Assert.Equal(0, rotator.SetPositionCallCount);
    }

    [Fact]
    public void Manual_move_applies_calibration_offsets()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            AzimuthOffsetDeg = 1,
            ElevationOffsetDeg = 0.5
        };

        controller.SetStandby(true, settings);
        controller.DrainCommandQueueForTests();
        controller.MoveTo(90, 30, settings);
        controller.DrainCommandQueueForTests();

        Assert.Equal(91, rotator.LastAzimuthDeg);
        Assert.Equal(30.5, rotator.LastElevationDeg);
    }

    [Fact]
    public void Manual_move_during_standby_is_not_re_parked()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            ParkAzimuthDeg = 0,
            ParkElevationDeg = 0
        };

        controller.SetStandby(true, settings);
        controller.DrainCommandQueueForTests();
        var callsAfterPark = rotator.SetPositionCallCount;

        controller.MoveTo(90, 45, settings);
        controller.DrainCommandQueueForTests();
        Assert.Equal(90, rotator.LastAzimuthDeg);
        Assert.Equal(45, rotator.LastElevationDeg);

        controller.UpdateSynchronously(settings, null);
        Assert.Equal(90, rotator.LastAzimuthDeg);
        Assert.Equal(callsAfterPark + 1, rotator.SetPositionCallCount);
    }

    [Fact]
    public void Stop_during_standby_sends_stop_command()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings { Enabled = true, Port = "COM3" };

        controller.SetStandby(true, settings);
        controller.DrainCommandQueueForTests();
        controller.Stop(settings);
        controller.DrainCommandQueueForTests();

        Assert.Equal(1, rotator.StopCallCount);
        Assert.False(controller.GetPositionStatus().IsTrackingHeld);
    }

    [Fact]
    public void Stop_during_tracking_sends_stop_and_holds_tracking()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            TrackStartElevationDeg = 5
        };

        controller.UpdateSynchronously(settings, TrackTarget("25544", 45, 20));
        Assert.Equal(1, rotator.SetPositionCallCount);

        controller.Stop(settings);
        controller.DrainCommandQueueForTests();

        Assert.Equal(1, rotator.StopCallCount);
        Assert.True(controller.GetPositionStatus().IsTrackingHeld);

        controller.UpdateSynchronously(settings, TrackTarget("25544", 90, 25));
        Assert.Equal(1, rotator.SetPositionCallCount);
    }

    [Fact]
    public void ResumeTracking_clears_hold_and_allows_tracking()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            TrackStartElevationDeg = 5
        };

        controller.UpdateSynchronously(settings, TrackTarget("25544", 45, 20));
        controller.Stop(settings);
        controller.DrainCommandQueueForTests();
        Assert.True(controller.GetPositionStatus().IsTrackingHeld);

        controller.ResumeTracking(settings);
        controller.DrainCommandQueueForTests();
        Assert.False(controller.GetPositionStatus().IsTrackingHeld);

        controller.UpdateSynchronously(settings, TrackTarget("25544", 90, 25));
        Assert.Equal(2, rotator.SetPositionCallCount);
    }

    [Fact]
    public void Park_when_already_parked_sends_park_position_again()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            ParkAzimuthDeg = 180,
            ParkElevationDeg = 0
        };

        controller.Park(settings);
        controller.DrainCommandQueueForTests();
        Assert.Equal(1, rotator.SetPositionCallCount);

        controller.Park(settings);
        controller.DrainCommandQueueForTests();

        Assert.Equal(2, rotator.SetPositionCallCount);
        Assert.Equal(180, rotator.LastAzimuthDeg);
        Assert.Equal(0, rotator.LastElevationDeg);
        Assert.True(controller.GetPositionStatus().IsParked);
    }

    [Fact]
    public void Park_after_stop_clears_tracking_hold()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            TrackStartElevationDeg = 5,
            ParkAzimuthDeg = 180,
            ParkElevationDeg = 0
        };

        controller.UpdateSynchronously(settings, TrackTarget("25544", 45, 20));
        controller.Stop(settings);
        controller.DrainCommandQueueForTests();
        Assert.True(controller.GetPositionStatus().IsTrackingHeld);

        controller.Park(settings);
        controller.DrainCommandQueueForTests();

        Assert.False(controller.GetPositionStatus().IsTrackingHeld);
        Assert.Equal(180, rotator.LastAzimuthDeg);
        Assert.True(controller.GetPositionStatus().IsParked);
    }

    [Fact]
    public void Park_during_standby_sends_park_position()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            ParkAzimuthDeg = 180,
            ParkElevationDeg = 10
        };

        controller.SetStandby(true, settings);
        controller.DrainCommandQueueForTests();
        controller.MoveTo(90, 45, settings);
        controller.DrainCommandQueueForTests();
        controller.Park(settings);
        controller.DrainCommandQueueForTests();

        Assert.Equal(180, rotator.LastAzimuthDeg);
        Assert.Equal(10, rotator.LastElevationDeg);
    }

    [Fact]
    public void PublishTarget_is_non_blocking()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings { Enabled = true, Port = "COM3" };
        var target = new SatelliteTrackState
        {
            Name = "TEST",
            NoradId = "1",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(10, 15, 500, 0)
        };

        controller.Update(settings, target);
        Assert.Equal(0, rotator.SetPositionCallCount);

        controller.DrainCommandQueueForTests();
        controller.UpdateSynchronously(settings, target);

        Assert.True(rotator.SetPositionCallCount >= 1);
    }

    [Fact]
    public void Disabled_settings_disconnects_on_worker()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings { Enabled = true, Port = "COM3" };
        var target = new SatelliteTrackState
        {
            Name = "TEST",
            NoradId = "1",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(10, 15, 500, 0)
        };

        controller.UpdateSynchronously(settings, target);
        Assert.True(controller.GetPositionStatus().IsConnected);

        controller.UpdateSynchronously(new RotatorSettings { Enabled = false }, null);
        Assert.False(controller.GetPositionStatus().IsConnected);
    }

    [Fact]
    public void Smart450_uses_extended_azimuth_at_north_wrap()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            AzimuthRange = RotatorAzimuthRange.Deg450,
            SmartAzimuth450 = true,
            TrackStartElevationDeg = 5
        };

        var norad = "25544";
        controller.UpdateSynchronously(settings, TrackTarget(norad, 350, 20));
        Assert.Equal(350, rotator.LastAzimuthDeg);

        controller.UpdateSynchronously(settings, TrackTarget(norad, 10, 20));
        Assert.Equal(370, rotator.LastAzimuthDeg);

        var status = controller.GetPositionStatus();
        Assert.Equal(370, status.CommandedAzimuthDeg);
        Assert.Equal(10, status.CompassAzimuthDeg);
    }

    [Fact]
    public void Smart450_with_negative_azimuth_offset_wraps_in_compass_space()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            AzimuthRange = RotatorAzimuthRange.Deg450,
            SmartAzimuth450 = true,
            TrackStartElevationDeg = 5,
            AzimuthOffsetDeg = -72
        };

        var norad = "25544";
        controller.UpdateSynchronously(settings, TrackTarget(norad, 350, 20));
        Assert.Equal(278, rotator.LastAzimuthDeg);

        controller.UpdateSynchronously(settings, TrackTarget(norad, 11, 20));
        Assert.Equal(299, rotator.LastAzimuthDeg);
    }

    [Fact]
    public void Smart450_disabled_uses_compass_azimuth_at_north_wrap()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            AzimuthRange = RotatorAzimuthRange.Deg450,
            SmartAzimuth450 = false,
            TrackStartElevationDeg = 5
        };

        var norad = "25544";
        controller.UpdateSynchronously(settings, TrackTarget(norad, 350, 20));
        controller.UpdateSynchronously(settings, TrackTarget(norad, 10, 20));
        Assert.Equal(10, rotator.LastAzimuthDeg);
    }

    [Fact]
    public void Smart450_uses_polled_position_after_target_change()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            AzimuthRange = RotatorAzimuthRange.Deg450,
            SmartAzimuth450 = true,
            TrackStartElevationDeg = 5
        };

        controller.UpdateSynchronously(settings, TrackTarget("1", 350, 20));
        controller.UpdateSynchronously(settings, TrackTarget("2", 15, 20));
        Assert.Equal(375, rotator.LastAzimuthDeg);
    }

    [Fact]
    public void Smart450_west_side_north_wrap_after_tca()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            AzimuthRange = RotatorAzimuthRange.Deg450,
            SmartAzimuth450 = true,
            TrackStartElevationDeg = 5
        };

        var norad = "25544";
        controller.UpdateSynchronously(settings, TrackTarget(norad, 15, 45));
        controller.UpdateSynchronously(settings, TrackTarget(norad, 330, 30));
        Assert.Contains(375, rotator.AzimuthHistory);
        Assert.Equal(330, rotator.LastAzimuthDeg);
    }

    [Fact]
    public void Smart450_mid_pass_join_at_34_deg_uses_extended_before_west_jump()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            AzimuthRange = RotatorAzimuthRange.Deg450,
            SmartAzimuth450 = true,
            TrackStartElevationDeg = 5
        };

        var norad = "25544";
        controller.UpdateSynchronously(settings, TrackTarget("other", 180, 30));
        controller.UpdateSynchronously(settings, TrackTarget(norad, 34, 25));
        controller.UpdateSynchronously(settings, TrackTarget(norad, 330, 20, aheadAzimuthDeg: 325));
        Assert.Contains(394, rotator.AzimuthHistory);
        Assert.Equal(330, rotator.LastAzimuthDeg);
    }

    [Fact]
    public void Smart450_east_side_north_crossing_commits_before_compass_wrap()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            AzimuthRange = RotatorAzimuthRange.Deg450,
            SmartAzimuth450 = true,
            TrackStartElevationDeg = 5
        };

        var norad = "25544";
        controller.UpdateSynchronously(settings, TrackTarget(norad, 80, 20));
        controller.UpdateSynchronously(settings, TrackTarget(norad, 50, 20));
        controller.UpdateSynchronously(settings, TrackTarget(norad, 25, 20));
        controller.UpdateSynchronously(settings, TrackTarget(norad, 20, 20, aheadAzimuthDeg: 355));
        Assert.Equal(380, rotator.LastAzimuthDeg);

        controller.UpdateSynchronously(settings, TrackTarget(norad, 15, 20, aheadAzimuthDeg: 355));
        controller.UpdateSynchronously(settings, TrackTarget(norad, 355, 20));
        Assert.Equal(355, rotator.LastAzimuthDeg);
        Assert.InRange(Math.Abs(rotator.LastAzimuthDeg!.Value - 380), 0, 30);
    }

    [Fact]
    public void Azimuth360_does_not_use_extended_azimuth()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            AzimuthRange = RotatorAzimuthRange.Deg360,
            SmartAzimuth450 = true,
            TrackStartElevationDeg = 5
        };

        var norad = "25544";
        controller.UpdateSynchronously(settings, TrackTarget(norad, 350, 20));
        controller.UpdateSynchronously(settings, TrackTarget(norad, 10, 20));
        Assert.Equal(10, rotator.LastAzimuthDeg);
    }

    [Fact]
    public void Smart450_park_135_southeast_pass_stays_on_primary_band()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            AzimuthRange = RotatorAzimuthRange.Deg450,
            SmartAzimuth450 = true,
            TrackStartElevationDeg = 5,
            ParkAzimuthDeg = 135,
            ParkElevationDeg = 0
        };

        var norad = "44909";
        // Mast already at park 135° (commanded/polled), then AOS from the north going SE.
        controller.UpdateSynchronously(settings, TrackTarget(norad, 135, 6));
        Assert.Equal(135, rotator.LastAzimuthDeg);

        controller.UpdateSynchronously(settings, TrackTarget(norad, 3, 10, aheadAzimuthDeg: 10));
        Assert.Equal(3, rotator.LastAzimuthDeg);

        controller.UpdateSynchronously(settings, TrackTarget(norad, 15, 20, aheadAzimuthDeg: 25));
        controller.UpdateSynchronously(settings, TrackTarget(norad, 45, 30, aheadAzimuthDeg: 60));
        controller.UpdateSynchronously(settings, TrackTarget(norad, 90, 25, aheadAzimuthDeg: 120));
        Assert.Equal(90, rotator.LastAzimuthDeg);
        Assert.All(rotator.AzimuthHistory, az => Assert.True(az <= 360));
    }

    [Fact]
    public void Smart450_prefers_polled_position_when_mast_moved_outside_app()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            AzimuthRange = RotatorAzimuthRange.Deg450,
            SmartAzimuth450 = true,
            TrackStartElevationDeg = 5
        };

        var norad = "44909";
        controller.UpdateSynchronously(settings, TrackTarget(norad, 350, 20));
        controller.UpdateSynchronously(settings, TrackTarget(norad, 10, 20));
        Assert.Equal(370, rotator.LastAzimuthDeg);

        // Operator wind-parked to 135° without telling OscarWatch; last command still in overlap.
        rotator.PolledAzimuthOverride = 135;
        controller.UpdateSynchronously(settings, TrackTarget(norad, 3, 15, aheadAzimuthDeg: 10));
        Assert.Equal(3, rotator.LastAzimuthDeg);
        Assert.DoesNotContain(363, rotator.AzimuthHistory);
    }

    [Fact]
    public void Smart450_overlap_then_southeast_does_not_climb_to_450()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            AzimuthRange = RotatorAzimuthRange.Deg450,
            SmartAzimuth450 = true,
            TrackStartElevationDeg = 5
        };

        var norad = "44909";
        controller.UpdateSynchronously(settings, TrackTarget(norad, 350, 20));
        controller.UpdateSynchronously(settings, TrackTarget(norad, 10, 20));
        Assert.Equal(370, rotator.LastAzimuthDeg);

        controller.UpdateSynchronously(settings, TrackTarget(norad, 25, 25, aheadAzimuthDeg: 40));
        controller.UpdateSynchronously(settings, TrackTarget(norad, 60, 30, aheadAzimuthDeg: 90));
        controller.UpdateSynchronously(settings, TrackTarget(norad, 120, 20, aheadAzimuthDeg: 135));

        Assert.All(rotator.AzimuthHistory, az => Assert.True(az < 420));
        Assert.Equal(120, rotator.LastAzimuthDeg);
    }

    [Fact]
    public void Smart450_pass_plan_primary_band_does_not_force_catastrophic_jump()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            AzimuthRange = RotatorAzimuthRange.Deg450,
            SmartAzimuth450 = true,
            TrackStartElevationDeg = 5
        };

        var aos = DateTime.UtcNow.AddMinutes(-1);
        var plan = new SmartAzimuthPassPlan(
            aos,
            aos.AddMinutes(10),
            [
                new SmartAzimuthPassSample(aos, SmartAzimuthBand.Primary),
                new SmartAzimuthPassSample(aos.AddMinutes(5), SmartAzimuthBand.Primary)
            ]);

        controller.UpdateSynchronously(settings, TrackTarget("44909", 350, 20));
        controller.SetSmartAzimuthPlanForTests(plan);
        // Preferred Primary would command 10 (|10−350|>180); fall back to tick resolve (370).
        controller.UpdateSynchronously(settings, TrackTarget("44909", 10, 20));
        Assert.Equal(370, rotator.LastAzimuthDeg);
    }

    [Fact]
    public void Smart450_pass_plan_extended_band_forces_overlap()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            AzimuthRange = RotatorAzimuthRange.Deg450,
            SmartAzimuth450 = true,
            TrackStartElevationDeg = 5
        };

        var aos = DateTime.UtcNow.AddMinutes(-1);
        var plan = new SmartAzimuthPassPlan(
            aos,
            aos.AddMinutes(10),
            [
                new SmartAzimuthPassSample(aos, SmartAzimuthBand.Extended),
                new SmartAzimuthPassSample(aos.AddMinutes(5), SmartAzimuthBand.Extended)
            ]);

        // Last command near north so Extended (375) is a short dial step, not a >180° yank.
        controller.UpdateSynchronously(settings, TrackTarget("44909", 350, 20));
        controller.SetSmartAzimuthPlanForTests(plan);
        controller.UpdateSynchronously(settings, TrackTarget("44909", 15, 25));
        Assert.Equal(375, rotator.LastAzimuthDeg);
    }

    [Fact]
    public void Smart450_aos_handoff_primary_plan_keeps_southeast_on_primary()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            AzimuthRange = RotatorAzimuthRange.Deg450,
            SmartAzimuth450 = true,
            TrackStartElevationDeg = -3,
            ParkAzimuthDeg = 135,
            ParkElevationDeg = 0
        };

        var aos = DateTime.UtcNow.AddMinutes(-1);
        var plan = new SmartAzimuthPassPlan(
            aos,
            aos.AddMinutes(10),
            [
                new SmartAzimuthPassSample(aos, SmartAzimuthBand.Primary),
                new SmartAzimuthPassSample(aos.AddMinutes(5), SmartAzimuthBand.Primary)
            ]);

        // Pass plan before first track (production order after MainViewModel change).
        controller.SetSmartAzimuthPlanForTests(plan);
        controller.UpdateSynchronously(settings, TrackTarget("44909", 3, 5, aheadAzimuthDeg: 10));
        Assert.Equal(3, rotator.LastAzimuthDeg);

        controller.UpdateSynchronously(settings, TrackTarget("44909", 15, 10, aheadAzimuthDeg: 25));
        controller.UpdateSynchronously(settings, TrackTarget("44909", 45, 15, aheadAzimuthDeg: 60));
        Assert.Equal(45, rotator.LastAzimuthDeg);
        Assert.All(rotator.AzimuthHistory, az => Assert.True(az <= 360));
    }

    [Fact]
    public void Missing_look_angles_after_track_waits_grace_before_park()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            TrackStartElevationDeg = -3,
            ParkAzimuthDeg = 180,
            ParkElevationDeg = 0,
            ParkAfterPass = true
        };

        controller.UpdateSynchronously(settings, TrackTarget("25544", 45, 10));
        Assert.Equal(45, rotator.LastAzimuthDeg);
        Assert.Equal(10, rotator.LastElevationDeg);

        var callsAfterTrack = rotator.SetPositionCallCount;

        // One missing look-angles tick must not slam El to park.
        controller.UpdateSynchronously(settings, TargetWithoutLookAngles("25544"));
        Assert.Equal(callsAfterTrack, rotator.SetPositionCallCount);
        Assert.Equal(10, rotator.LastElevationDeg);

        for (var i = 1; i < RotatorController.MissingLookAnglesParkGraceTicks; i++)
            controller.UpdateSynchronously(settings, TargetWithoutLookAngles("25544"));

        Assert.Equal(180, rotator.LastAzimuthDeg);
        Assert.Equal(0, rotator.LastElevationDeg);
        Assert.True(controller.GetPositionStatus().IsParked);
    }

    [Fact]
    public void Missing_look_angles_before_track_still_parks_immediately()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            TrackStartElevationDeg = -3,
            ParkAzimuthDeg = 180,
            ParkElevationDeg = 0,
            ParkAfterPass = true
        };

        controller.UpdateSynchronously(settings, TargetWithoutLookAngles("25544"));
        Assert.Equal(180, rotator.LastAzimuthDeg);
        Assert.Equal(0, rotator.LastElevationDeg);
        Assert.True(controller.GetPositionStatus().IsParked);
    }

    private static SatelliteTrackState TrackTarget(
        string noradId,
        double azimuthDeg,
        double elevationDeg,
        double? aheadAzimuthDeg = null) =>
        new()
        {
            Name = "TEST",
            NoradId = noradId,
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(azimuthDeg, elevationDeg, 800, 0),
            AheadAzimuthDeg = aheadAzimuthDeg
        };

    private static SatelliteTrackState TargetWithoutLookAngles(string noradId) =>
        new()
        {
            Name = "TEST",
            NoradId = noradId,
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = null
        };
}
