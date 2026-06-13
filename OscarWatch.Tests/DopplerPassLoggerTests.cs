using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using OscarWatch.Diagnostics;

namespace OscarWatch.Tests;

public class DopplerPassLoggerTests
{
    [Fact]
    public void FormatEntry_escapes_commas_in_notes()
    {
        var entry = SampleEntry(notes: "rx=0.000->-0.100;tx=0.000->0.000");

        var line = DopplerPassLogger.FormatEntry(entry);

        Assert.Contains("rx=0.000->-0.100;tx=0.000->0.000", line);
        Assert.StartsWith("2026-06-12 12:00:00.000,offset_change", line);
    }

    [Fact]
    public void BeginPass_writes_header_and_settings_comment()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "OscarWatchTests", Guid.NewGuid().ToString("N"));
        var logger = new DopplerPassLogger(tempDir);
        var settings = new RigSettings
        {
            DopplerPassLogEnabled = true,
            DopplerThresholdLinearHz = 50,
            DopplerCatLeadEnabled = true,
            DopplerCatLeadGainPercent = 70,
            DopplerCatLeadMs = 40,
            DopplerAdaptiveThresholdEnabled = true
        };
        var context = SampleContext();

        logger.BeginPass(settings, context, new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc));

        Assert.NotNull(logger.ActiveLogPath);
        var path = logger.ActiveLogPath!;
        logger.EndPass(DateTime.UtcNow, "test");
        var text = File.ReadAllText(path);
        Assert.Contains("Utc,Event,NoradId", text);
        Assert.Contains("# pass_start", text);
        Assert.Contains("lead_gain=70", text);
        Assert.Contains("adaptive=True", text);

        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, recursive: true);
    }

    [Fact]
    public void Capture_populates_lead_and_threshold_fields()
    {
        var settings = new RigSettings
        {
            DopplerCatLeadEnabled = true,
            DopplerCatLeadGainPercent = 70,
            DopplerThresholdLinearHz = 50,
            DopplerAdaptiveThresholdEnabled = true
        };
        var context = SampleContext();
        var corrected = new CorrectedFrequencies(145_950, 435_667, 145_950, 435_667, 0, false);

        var entry = DopplerDiagnostics.Capture(
            propagator: null,
            settings,
            new GroundStation(),
            context,
            DateTime.UtcNow,
            baseThresholdHz: 50,
            effectiveThresholdHz: 25,
            corrected,
            lastRigRxHz: 435_666_900,
            lastRigTxHz: 145_950_100,
            passbandDlKHz: 0,
            passbandUlKHz: 0,
            eventName: "snapshot",
            belowThreshold: true);

        Assert.Equal("snapshot", entry.Event);
        Assert.Equal(50, entry.BaseThresholdHz);
        Assert.Equal(25, entry.EffectiveThresholdHz);
        Assert.True(entry.BelowThreshold);
        Assert.Equal(70, entry.LeadGainPercent);
    }

    private static RigTrackingContext SampleContext() =>
        new()
        {
            TrackState = new SatelliteTrackState
            {
                Name = "FO-29",
                NoradId = "24278",
                Subpoint = new GeoCoordinate(58, -4, 600),
                LookAngles = new LookAngles(180, 30, 800, 1.0)
            },
            Mode = new SatelliteTransponderMode
            {
                Type = "USB",
                DownlinkKHz = 435_800,
                UplinkKHz = 145_920,
                DownlinkMode = "USB",
                UplinkMode = "USB",
                Doppler = "NOR"
            },
            Corrected = new CorrectedFrequencies(145_920, 435_800, 145_920, 435_800, 0, false)
        };

    private static DopplerPassLogEntry SampleEntry(string? notes = null) =>
        new(
            Utc: new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc),
            Event: "offset_change",
            NoradId: "99999",
            SatelliteName: "RS-44",
            ElevationDeg: 45,
            AzimuthDeg: 180,
            RangeRateKmPerSec: 1.2,
            SlopeKmPerSec2: 0.012,
            SlewHzPerSec: 17.4,
            BaseThresholdHz: 50,
            EffectiveThresholdHz: 38,
            LeadEnabled: true,
            LeadBlend: 0.55,
            LeadGainPercent: 70,
            LeadMsRx: 40,
            LeadMsTx: 40,
            LeadRxRangeRate: 1.25,
            LeadTxRangeRate: 1.25,
            SatRxKHz: 435_667,
            SatTxKHz: 145_950,
            RadioRxKHz: 435_667.1,
            RadioTxKHz: 145_949.9,
            LastRigRxHz: 435_667_000,
            LastRigTxHz: 145_950_000,
            RxDeltaHz: 100,
            TxDeltaHz: 100,
            RxOffsetKHz: -0.1,
            TxOffsetKHz: 0,
            PassbandDlKHz: 0,
            PassbandUlKHz: 0,
            WroteRx: false,
            WroteTx: false,
            BelowThreshold: true,
            Interactive: true,
            CatPaused: false,
            Notes: notes);
}
