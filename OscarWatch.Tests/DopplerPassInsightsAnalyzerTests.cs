using OscarWatch.Localization;
using OscarWatch.ViewModels;

namespace OscarWatch.Tests;

public class DopplerPassInsightsAnalyzerTests
{
    private static readonly ILocalizationService L = LocalizationService.Instance;

    [Fact]
    public void ParseSettingsComment_reads_pass_settings_line()
    {
        var line = "# settings,threshold_linear=50,threshold_fm=350,cat_delay_ms=50,lead=True,lead_ms=0,lead_gain=100,adaptive=True";

        var settings = DopplerPassInsightsAnalyzer.ParseSettingsComment(line);

        Assert.NotNull(settings);
        Assert.Equal(50, settings!.ThresholdLinearHz);
        Assert.Equal(350, settings.ThresholdFmHz);
        Assert.Equal(50, settings.CatDelayMs);
        Assert.True(settings.LeadEnabled);
        Assert.Equal(100, settings.LeadGainPercent);
        Assert.True(settings.AdaptiveThresholdEnabled);
    }

    [Fact]
    public void Assess_flags_high_threshold_when_mostly_suppressed()
    {
        var rows = BuildPassRows(count: 20, belowThreshold: 18, wroteRx: 1, slew: 10, leadEnabled: true);

        var assessment = DopplerPassInsightsAnalyzer.Assess(L, rows, SampleSettings());

        Assert.Contains(assessment.Recommendations, r => r.Contains("lower", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Threshold", assessment.VerdictTitle, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Assess_suggests_lead_when_slew_high_and_lead_disabled()
    {
        var rows = BuildPassRows(count: 20, belowThreshold: 2, wroteRx: 10, slew: 280, leadEnabled: false);
        var settings = SampleSettings() with { LeadEnabled = false };

        var assessment = DopplerPassInsightsAnalyzer.Assess(L, rows, settings);

        Assert.Contains(assessment.Recommendations, r => r.Contains("Lead", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Enable", assessment.LeadAction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildPassPhases_returns_ordered_segments()
    {
        var start = new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);
        var rows = new List<DopplerInsightCsvRow>();
        for (var i = 0; i < 50; i++)
        {
            var elev = i < 25 ? i * 2.0 : (50 - i) * 2.0;
            rows.Add(SampleRow(start.AddSeconds(i * 6), elev, slew: 30, belowThreshold: false, wroteRx: i % 4 == 0));
        }

        var phases = DopplerPassInsightsAnalyzer.BuildPassPhases(L, rows);

        Assert.True(phases.Count >= 3);
        Assert.Contains(phases, p => p.Label.Contains("TCA", StringComparison.OrdinalIgnoreCase));
        Assert.All(phases, p => Assert.False(string.IsNullOrWhiteSpace(p.Summary)));
    }

    private static DopplerPassSettingsSnapshot SampleSettings() =>
        new(ThresholdLinearHz: 50, ThresholdFmHz: 350, CatDelayMs: 50, LeadEnabled: true, LeadMs: 0, LeadGainPercent: 100, AdaptiveThresholdEnabled: true);

    private static List<DopplerInsightCsvRow> BuildPassRows(int count, int belowThreshold, int wroteRx, double slew, bool leadEnabled)
    {
        var start = new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);
        var rows = new List<DopplerInsightCsvRow>();
        for (var i = 0; i < count; i++)
        {
            rows.Add(SampleRow(
                start.AddSeconds(i),
                elevation: 10 + i,
                slew: slew,
                belowThreshold: i < belowThreshold,
                wroteRx: i < wroteRx,
                leadEnabled: leadEnabled));
        }

        return rows;
    }

    private static DopplerInsightCsvRow SampleRow(
        DateTime utc,
        double elevation,
        double slew,
        bool belowThreshold,
        bool wroteRx,
        bool leadEnabled = true)
    {
        return new DopplerInsightCsvRow(
            Utc: utc,
            Event: wroteRx ? "cat_write" : "snapshot",
            SatelliteName: "AO-91",
            ElevationDeg: elevation,
            BaseThresholdHz: 50,
            EffectiveThresholdHz: belowThreshold ? 50 : 25,
            SlewHzPerSec: slew,
            RxDeltaHz: belowThreshold ? 20 : 120,
            TxDeltaHz: 30,
            WroteRx: wroteRx,
            WroteTx: false,
            BelowThreshold: belowThreshold,
            Interactive: false,
            CatPaused: false,
            LeadEnabled: leadEnabled,
            Notes: "");
    }
}
