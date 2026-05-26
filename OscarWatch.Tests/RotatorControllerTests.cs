using OscarWatch.Core.Models;
using OscarWatch.Rotator;

namespace OscarWatch.Tests;

public sealed class RotatorControllerTests
{
    [Fact]
    public void Update_runs_on_worker_thread_and_tracks_satellite()
    {
        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
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
}
