using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

public class RigControllerDopplerLogTests
{
    [Fact]
    public void Doppler_pass_log_does_not_start_below_horizon()
    {
        var logger = new RecordingDopplerPassLogger();
        var controller = new RigController(dopplerPassLogger: logger);
        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.Dummy,
            DopplerPassLogEnabled = true
        };

        controller.Update(settings, ContextWithElevation(-48));

        Assert.Null(logger.ActiveLogPath);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void Doppler_pass_log_starts_at_horizon_and_ends_after_los()
    {
        var logger = new RecordingDopplerPassLogger();
        var controller = new RigController(dopplerPassLogger: logger);
        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.Dummy,
            DopplerPassLogEnabled = true,
            CatDelayMs = 0,
            DopplerThresholdLinearHz = 0
        };

        controller.Update(settings, ContextWithElevation(25));
        Assert.NotNull(logger.ActiveLogPath);

        controller.Update(settings, ContextWithElevation(-5));
        Assert.Null(logger.ActiveLogPath);
    }

    [Fact]
    public void Doppler_pass_log_records_cat_pause_transition_and_heartbeat()
    {
        var logger = new RecordingDopplerPassLogger();
        var controller = new RigController(dopplerPassLogger: logger);
        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.Dummy,
            DopplerPassLogEnabled = true,
            CatDelayMs = 0,
            DopplerThresholdLinearHz = 0
        };
        var context = ContextWithElevation(25);

        controller.Update(settings, context);
        Assert.NotNull(logger.ActiveLogPath);

        settings.CatUpdatesPaused = true;
        controller.Update(settings, context);
        Assert.Contains(logger.Entries, e => e.Event == "cat_pause_start");
        Assert.True(logger.Entries.Single(e => e.Event == "cat_pause_start").CatPaused);
        Assert.Equal("cat_paused", logger.Entries.Single(e => e.Event == "cat_pause_start").SkipReason);
        Assert.False(logger.Entries.Single(e => e.Event == "cat_pause_start").RigTracking);

        Thread.Sleep(1100);
        controller.RunTrackingLoopOnce();
        Assert.Contains(logger.Entries, e => e.Event == "cat_paused");

        settings.CatUpdatesPaused = false;
        controller.Update(settings, context);
        Assert.Contains(logger.Entries, e => e.Event == "cat_pause_end");
    }

    private static RigTrackingContext ContextWithElevation(double elevationDeg)
    {
        var mode = new SatelliteTransponderMode
        {
            Type = "USB",
            DownlinkKHz = 435_800,
            UplinkKHz = 145_920,
            DownlinkMode = "USB",
            UplinkMode = "USB",
            Doppler = "NOR"
        };

        var state = new SatelliteTrackState
        {
            Name = "FO-29",
            NoradId = "24278",
            Subpoint = new GeoCoordinate(58, -4, 600),
            LookAngles = new LookAngles(180, elevationDeg, 800, 1.0)
        };

        return new RigTrackingContext
        {
            TrackState = state,
            Mode = mode,
            Corrected = new CorrectedFrequencies(145_920, 435_800, 145_920, 435_800, 0, false)
        };
    }

    private sealed class RecordingDopplerPassLogger : IDopplerPassLogger
    {
        public List<DopplerPassLogEntry> Entries { get; } = [];
        public string? ActiveLogPath { get; private set; }
        public string LogDirectory { get; } = "";

        public void BeginPass(RigSettings settings, RigTrackingContext context, DateTime utc) =>
            ActiveLogPath = "recording://test";

        public void Append(DopplerPassLogEntry entry) => Entries.Add(entry);

        public void EndPass(DateTime utc, string? reason = null) => ActiveLogPath = null;
    }
}
