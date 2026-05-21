using OscarWatch.Core.Models;
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
    public void Icom9700_reports_not_implemented()
    {
        var controller = new RigController();
        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.IcomIc9700,
            Port = "COM99"
        };
        controller.Update(settings, null);
        Assert.Contains("not yet implemented", controller.GetStatus().StatusMessage ?? "", StringComparison.OrdinalIgnoreCase);
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
        Assert.False(controller.GetStatus().IsTracking);
    }
}
