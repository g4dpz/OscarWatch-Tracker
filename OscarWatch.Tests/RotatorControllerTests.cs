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
}
