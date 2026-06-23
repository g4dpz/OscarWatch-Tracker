using System.Globalization;
using OscarWatch.Localization;

namespace OscarWatch.ViewModels;

internal static class DopplerPassInsightsAnalyzer
{
    public static DopplerPassSettingsSnapshot? ParseSettingsComment(string? line)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("# settings,", StringComparison.OrdinalIgnoreCase))
            return null;

        return new DopplerPassSettingsSnapshot(
            ThresholdLinearHz: ReadIntSetting(line, "threshold_linear"),
            ThresholdFmHz: ReadIntSetting(line, "threshold_fm"),
            CatDelayMs: ReadIntSetting(line, "cat_delay_ms"),
            LeadEnabled: ReadBoolSetting(line, "lead"),
            LeadMs: ReadIntSetting(line, "lead_ms"),
            LeadGainPercent: ReadIntSetting(line, "lead_gain"),
            AdaptiveThresholdEnabled: ReadBoolSetting(line, "adaptive"));
    }

    public static DopplerTuningAssessment Assess(
        ILocalizationService l,
        IReadOnlyList<DopplerInsightCsvRow> rows,
        DopplerPassSettingsSnapshot? settings)
    {
        if (rows.Count == 0)
        {
            return new DopplerTuningAssessment(
                VerdictTitle: l.Get("DopplerInsights.Verdict.EmptyTitle"),
                VerdictSummary: l.Get("DopplerInsights.Verdict.EmptySummary"),
                AdaptiveTitle: l.Get("DopplerInsights.Adaptive.EmptyTitle"),
                AdaptiveDetail: l.Get("DopplerInsights.Adaptive.EmptyDetail"),
                AdaptiveAction: "",
                LeadTitle: l.Get("DopplerInsights.Lead.EmptyTitle"),
                LeadDetail: l.Get("DopplerInsights.Lead.EmptyDetail"),
                LeadAction: "",
                SettingsUsedText: l.Get("DopplerInsights.Settings.Empty"),
                Recommendations: [l.Get("DopplerInsights.Reco.Empty")]);
        }

        var total = rows.Count;
        var rxWrites = rows.Count(r => r.WroteRx);
        var txWrites = rows.Count(r => r.WroteTx);
        var belowThresholdCount = rows.Count(r => r.BelowThreshold);
        var interactiveCount = rows.Count(r => r.Interactive);
        var catPausedCount = rows.Count(r => r.CatPaused);

        var belowThresholdPct = Percent(belowThresholdCount, total);
        var rxWritePct = Percent(rxWrites, total);
        var txWritePct = Percent(txWrites, total);
        var leadEnabledPct = Percent(rows.Count(r => r.LeadEnabled), total);

        var baseThresholdAvg = AverageOrZero(rows.Select(r => (double)r.BaseThresholdHz));
        var effectiveThresholdAvg = AverageOrZero(rows.Select(r => (double)r.EffectiveThresholdHz));
        var thresholdReductionPct = baseThresholdAvg <= 0
            ? 0
            : Math.Max(0, (baseThresholdAvg - effectiveThresholdAvg) / baseThresholdAvg * 100.0);

        var rxAbs = rows.Select(r => Math.Abs((double)r.RxDeltaHz)).ToList();
        var txAbs = rows.Select(r => Math.Abs((double)r.TxDeltaHz)).ToList();
        var maxSlew = rows.Select(r => Math.Abs(r.SlewHzPerSec)).DefaultIfEmpty(0).Max();
        var rxP95 = P95(rxAbs);
        var txP95 = P95(txAbs);

        var recommendations = BuildRecommendations(
            l,
            belowThresholdPct,
            rxWritePct,
            txWritePct,
            interactiveCount,
            catPausedCount,
            leadEnabledPct,
            maxSlew,
            thresholdReductionPct,
            settings);

        var adaptive = AssessAdaptive(
            l,
            settings,
            belowThresholdPct,
            rxWritePct,
            thresholdReductionPct,
            maxSlew,
            effectiveThresholdAvg);

        var lead = AssessLead(
            l,
            settings,
            leadEnabledPct,
            maxSlew,
            rxP95,
            txP95,
            rows);

        var verdict = AssessVerdict(
            l,
            rxP95,
            txP95,
            belowThresholdPct,
            rxWritePct,
            txWritePct,
            interactiveCount,
            catPausedCount,
            maxSlew,
            leadEnabledPct,
            settings);

        var settingsText = FormatSettingsUsed(l, settings, baseThresholdAvg, leadEnabledPct, thresholdReductionPct);

        return new DopplerTuningAssessment(
            VerdictTitle: verdict.Title,
            VerdictSummary: verdict.Summary,
            AdaptiveTitle: adaptive.Title,
            AdaptiveDetail: adaptive.Detail,
            AdaptiveAction: adaptive.Action,
            LeadTitle: lead.Title,
            LeadDetail: lead.Detail,
            LeadAction: lead.Action,
            SettingsUsedText: settingsText,
            Recommendations: recommendations);
    }

    public static IReadOnlyList<DopplerPassPhaseSummary> BuildPassPhases(
        ILocalizationService l,
        IReadOnlyList<DopplerInsightCsvRow> rows)
    {
        if (rows.Count == 0)
            return [];

        var sorted = rows
            .Where(r => r.Utc > DateTime.MinValue)
            .OrderBy(r => r.Utc)
            .ToList();
        if (sorted.Count == 0)
            return [];

        var startUtc = sorted[0].Utc;
        var endUtc = sorted[^1].Utc;
        var durationSec = Math.Max(1, (endUtc - startUtc).TotalSeconds);

        var tcaRow = sorted.OrderByDescending(r => r.ElevationDeg).First();
        var tcaSeconds = (tcaRow.Utc - startUtc).TotalSeconds;

        var phaseDefs = new (string Key, double StartFrac, double EndFrac)[]
        {
            ("Aos", 0.0, 0.2),
            ("Rising", 0.2, FractionBeforeTca(tcaSeconds, durationSec)),
            ("Tca", FractionBeforeTca(tcaSeconds, durationSec), FractionAfterTca(tcaSeconds, durationSec)),
            ("Descending", FractionAfterTca(tcaSeconds, durationSec), 0.8),
            ("Los", 0.8, 1.0)
        };

        var phases = new List<DopplerPassPhaseSummary>();
        foreach (var (key, startFrac, endFrac) in phaseDefs)
        {
            if (endFrac <= startFrac)
                continue;

            var phaseStart = startUtc.AddSeconds(durationSec * startFrac);
            var phaseEnd = startUtc.AddSeconds(durationSec * endFrac);
            var phaseRows = sorted.Where(r => r.Utc >= phaseStart && r.Utc < phaseEnd).ToList();
            if (phaseRows.Count == 0)
                continue;

            var phaseRxP95 = P95(phaseRows.Select(r => Math.Abs((double)r.RxDeltaHz)));
            var phaseWritePct = Percent(phaseRows.Count(r => r.WroteRx || r.WroteTx), phaseRows.Count);
            var phaseSlew = phaseRows.Select(r => Math.Abs(r.SlewHzPerSec)).DefaultIfEmpty(0).Max();
            var phaseElev = phaseRows.Max(r => r.ElevationDeg);
            var phaseLeadPct = Percent(phaseRows.Count(r => r.LeadEnabled), phaseRows.Count);

            phases.Add(new DopplerPassPhaseSummary(
                Label: l.Get($"DopplerInsights.Phase.{key}"),
                Summary: l.Get(
                    "DopplerInsights.Phase.Summary",
                    phaseRxP95.ToString("0", CultureInfo.InvariantCulture),
                    phaseWritePct.ToString("0", CultureInfo.InvariantCulture),
                    phaseSlew.ToString("0", CultureInfo.InvariantCulture),
                    phaseElev.ToString("0.0", CultureInfo.InvariantCulture)),
                RxP95Hz: phaseRxP95,
                WriteRatePct: phaseWritePct,
                PeakSlewHzPerSec: phaseSlew,
                MaxElevationDeg: phaseElev,
                LeadActivePct: phaseLeadPct));
        }

        return phases;
    }

    private static (string Title, string Summary) AssessVerdict(
        ILocalizationService l,
        double rxP95,
        double txP95,
        double belowThresholdPct,
        double rxWritePct,
        double txWritePct,
        int interactiveCount,
        int catPausedCount,
        double maxSlew,
        double leadEnabledPct,
        DopplerPassSettingsSnapshot? settings)
    {
        if (interactiveCount > 0 || catPausedCount > 0)
        {
            return (
                l.Get("DopplerInsights.Verdict.ManualTitle"),
                l.Get("DopplerInsights.Verdict.ManualSummary", interactiveCount, catPausedCount));
        }

        if (belowThresholdPct >= 75 && rxWritePct < 12)
        {
            return (
                l.Get("DopplerInsights.Verdict.ThresholdTitle"),
                l.Get("DopplerInsights.Verdict.ThresholdSummary", belowThresholdPct.ToString("0", CultureInfo.InvariantCulture)));
        }

        if (maxSlew >= 250 && leadEnabledPct < 40 && settings?.LeadEnabled != true)
        {
            return (
                l.Get("DopplerInsights.Verdict.LeadTitle"),
                l.Get("DopplerInsights.Verdict.LeadSummary", rxP95.ToString("0", CultureInfo.InvariantCulture)));
        }

        if (rxWritePct >= 80 || txWritePct >= 80)
        {
            return (
                l.Get("DopplerInsights.Verdict.ChatterTitle"),
                l.Get("DopplerInsights.Verdict.ChatterSummary", rxWritePct.ToString("0", CultureInfo.InvariantCulture)));
        }

        return (
            l.Get("DopplerInsights.Verdict.GoodTitle"),
            l.Get("DopplerInsights.Verdict.GoodSummary", rxP95.ToString("0", CultureInfo.InvariantCulture), txP95.ToString("0", CultureInfo.InvariantCulture)));
    }

    private static (string Title, string Detail, string Action) AssessAdaptive(
        ILocalizationService l,
        DopplerPassSettingsSnapshot? settings,
        double belowThresholdPct,
        double rxWritePct,
        double thresholdReductionPct,
        double maxSlew,
        double effectiveThresholdAvg)
    {
        var enabled = settings?.AdaptiveThresholdEnabled ?? thresholdReductionPct > 5;
        if (!enabled)
        {
            return (
                l.Get("DopplerInsights.Adaptive.DisabledTitle"),
                l.Get("DopplerInsights.Adaptive.DisabledDetail", maxSlew.ToString("0", CultureInfo.InvariantCulture)),
                maxSlew >= 20
                    ? l.Get("DopplerInsights.Adaptive.DisabledAction")
                    : l.Get("DopplerInsights.Adaptive.DisabledNoAction"));
        }

        if (belowThresholdPct >= 70 && rxWritePct < 15)
        {
            return (
                l.Get("DopplerInsights.Adaptive.TooConservativeTitle"),
                l.Get("DopplerInsights.Adaptive.TooConservativeDetail",
                    belowThresholdPct.ToString("0", CultureInfo.InvariantCulture),
                    effectiveThresholdAvg.ToString("0", CultureInfo.InvariantCulture)),
                l.Get("DopplerInsights.Adaptive.TooConservativeAction"));
        }

        if (thresholdReductionPct > 15 && rxWritePct < 40)
        {
            return (
                l.Get("DopplerInsights.Adaptive.WorkingTitle"),
                l.Get("DopplerInsights.Adaptive.WorkingDetail",
                    thresholdReductionPct.ToString("0", CultureInfo.InvariantCulture),
                    maxSlew.ToString("0", CultureInfo.InvariantCulture)),
                l.Get("DopplerInsights.Adaptive.WorkingAction"));
        }

        return (
            l.Get("DopplerInsights.Adaptive.NeutralTitle"),
            l.Get("DopplerInsights.Adaptive.NeutralDetail",
                thresholdReductionPct.ToString("0", CultureInfo.InvariantCulture)),
            l.Get("DopplerInsights.Adaptive.NeutralAction"));
    }

    private static (string Title, string Detail, string Action) AssessLead(
        ILocalizationService l,
        DopplerPassSettingsSnapshot? settings,
        double leadEnabledPct,
        double maxSlew,
        double rxP95,
        double txP95,
        IReadOnlyList<DopplerInsightCsvRow> rows)
    {
        var enabled = settings?.LeadEnabled ?? leadEnabledPct > 50;
        var steepRows = rows.Where(r => Math.Abs(r.SlewHzPerSec) >= 25).ToList();
        var steepLeadPct = steepRows.Count == 0 ? 0 : Percent(steepRows.Count(r => r.LeadEnabled), steepRows.Count);
        var steepRxP95 = steepRows.Count == 0 ? 0 : P95(steepRows.Select(r => Math.Abs((double)r.RxDeltaHz)));

        if (!enabled)
        {
            return (
                l.Get("DopplerInsights.Lead.DisabledTitle"),
                l.Get("DopplerInsights.Lead.DisabledDetail", maxSlew.ToString("0", CultureInfo.InvariantCulture)),
                maxSlew >= 200
                    ? l.Get("DopplerInsights.Lead.DisabledAction")
                    : l.Get("DopplerInsights.Lead.DisabledNoAction"));
        }

        if (maxSlew >= 200 && steepRxP95 > rxP95 * 0.85 && steepLeadPct < 60)
        {
            return (
                l.Get("DopplerInsights.Lead.WeakTitle"),
                l.Get("DopplerInsights.Lead.WeakDetail",
                    steepRxP95.ToString("0", CultureInfo.InvariantCulture),
                    settings?.LeadGainPercent.ToString(CultureInfo.InvariantCulture) ?? "?"),
                l.Get("DopplerInsights.Lead.WeakAction"));
        }

        if (rxP95 <= 30 && maxSlew >= 150)
        {
            return (
                l.Get("DopplerInsights.Lead.WorkingTitle"),
                l.Get("DopplerInsights.Lead.WorkingDetail",
                    leadEnabledPct.ToString("0", CultureInfo.InvariantCulture),
                    rxP95.ToString("0", CultureInfo.InvariantCulture)),
                l.Get("DopplerInsights.Lead.WorkingAction"));
        }

        if (rxP95 > 60 && maxSlew >= 200)
        {
            return (
                l.Get("DopplerInsights.Lead.LaggingTitle"),
                l.Get("DopplerInsights.Lead.LaggingDetail",
                    rxP95.ToString("0", CultureInfo.InvariantCulture),
                    txP95.ToString("0", CultureInfo.InvariantCulture)),
                l.Get("DopplerInsights.Lead.LaggingAction",
                    settings?.LeadMs.ToString(CultureInfo.InvariantCulture) ?? "0",
                    settings?.LeadGainPercent.ToString(CultureInfo.InvariantCulture) ?? "100"));
        }

        return (
            l.Get("DopplerInsights.Lead.NeutralTitle"),
            l.Get("DopplerInsights.Lead.NeutralDetail",
                leadEnabledPct.ToString("0", CultureInfo.InvariantCulture),
                maxSlew.ToString("0", CultureInfo.InvariantCulture)),
            l.Get("DopplerInsights.Lead.NeutralAction"));
    }

    private static string FormatSettingsUsed(
        ILocalizationService l,
        DopplerPassSettingsSnapshot? settings,
        double baseThresholdAvg,
        double leadEnabledPct,
        double thresholdReductionPct)
    {
        if (settings is null)
        {
            return l.Get(
                "DopplerInsights.Settings.Inferred",
                baseThresholdAvg.ToString("0", CultureInfo.InvariantCulture),
                leadEnabledPct.ToString("0", CultureInfo.InvariantCulture),
                thresholdReductionPct.ToString("0", CultureInfo.InvariantCulture));
        }

        return l.Get(
            "DopplerInsights.Settings.Value",
            settings.ThresholdLinearHz,
            settings.CatDelayMs,
            settings.AdaptiveThresholdEnabled
                ? l.Get("DopplerInsights.Settings.Enabled")
                : l.Get("DopplerInsights.Settings.Disabled"),
            settings.LeadEnabled
                ? l.Get("DopplerInsights.Settings.Enabled")
                : l.Get("DopplerInsights.Settings.Disabled"),
            settings.LeadMs,
            settings.LeadGainPercent);
    }

    private static List<string> BuildRecommendations(
        ILocalizationService l,
        double belowThresholdPercent,
        double rxWritePercent,
        double txWritePercent,
        int interactiveCount,
        int catPausedCount,
        double leadEnabledPct,
        double maxSlew,
        double thresholdReductionPct,
        DopplerPassSettingsSnapshot? settings)
    {
        var recommendations = new List<string>();

        if (belowThresholdPercent >= 75 && rxWritePercent < 12)
            recommendations.Add(l.Get("DopplerInsights.Reco.ThresholdHigh"));

        if (rxWritePercent >= 80 || txWritePercent >= 80)
            recommendations.Add(l.Get("DopplerInsights.Reco.ThresholdLow"));

        if (interactiveCount > 0)
            recommendations.Add(l.Get("DopplerInsights.Reco.Interactive"));

        if (catPausedCount > 0)
            recommendations.Add(l.Get("DopplerInsights.Reco.CatPaused"));

        if (maxSlew >= 250 && leadEnabledPct < 40 && settings?.LeadEnabled != true)
            recommendations.Add(l.Get("DopplerInsights.Reco.EnableLead"));

        if (maxSlew >= 200 && settings?.LeadEnabled == true && leadEnabledPct < 50)
            recommendations.Add(l.Get("DopplerInsights.Reco.IncreaseLead"));

        if (thresholdReductionPct > 20 && belowThresholdPercent > 60)
            recommendations.Add(l.Get("DopplerInsights.Reco.AdaptiveHelping"));

        if (thresholdReductionPct < 5 && maxSlew >= 25 && settings?.AdaptiveThresholdEnabled != true)
            recommendations.Add(l.Get("DopplerInsights.Reco.TryAdaptive"));

        if (recommendations.Count == 0)
            recommendations.Add(l.Get("DopplerInsights.Reco.Balanced"));

        return recommendations;
    }

    private static double FractionBeforeTca(double tcaSeconds, double durationSec)
    {
        var frac = tcaSeconds / durationSec;
        return Math.Clamp(frac - 0.08, 0.15, 0.75);
    }

    private static double FractionAfterTca(double tcaSeconds, double durationSec)
    {
        var frac = tcaSeconds / durationSec;
        return Math.Clamp(frac + 0.08, 0.25, 0.85);
    }

    private static int ReadIntSetting(string line, string key)
    {
        var token = FindSettingToken(line, key);
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static bool ReadBoolSetting(string line, string key)
    {
        var token = FindSettingToken(line, key);
        return string.Equals(token, "True", StringComparison.OrdinalIgnoreCase) || token == "1";
    }

    private static string FindSettingToken(string line, string key)
    {
        var needle = key + "=";
        var start = line.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return "";

        start += needle.Length;
        var end = line.IndexOf(',', start);
        return end < 0 ? line[start..].Trim() : line[start..end].Trim();
    }

    private static double Percent(int count, int total) => total <= 0 ? 0 : count * 100.0 / total;

    private static double AverageOrZero(IEnumerable<double> values)
    {
        var list = values as IReadOnlyList<double> ?? values.ToList();
        return list.Count == 0 ? 0 : list.Average();
    }

    private static double P95(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        if (sorted.Length == 0)
            return 0;

        var index = (int)Math.Ceiling(sorted.Length * 0.95) - 1;
        index = Math.Clamp(index, 0, sorted.Length - 1);
        return sorted[index];
    }
}

internal sealed record DopplerPassSettingsSnapshot(
    int ThresholdLinearHz,
    int ThresholdFmHz,
    int CatDelayMs,
    bool LeadEnabled,
    int LeadMs,
    int LeadGainPercent,
    bool AdaptiveThresholdEnabled);

internal sealed record DopplerTuningAssessment(
    string VerdictTitle,
    string VerdictSummary,
    string AdaptiveTitle,
    string AdaptiveDetail,
    string AdaptiveAction,
    string LeadTitle,
    string LeadDetail,
    string LeadAction,
    string SettingsUsedText,
    IReadOnlyList<string> Recommendations);

public sealed record DopplerPassPhaseSummary(
    string Label,
    string Summary,
    double RxP95Hz,
    double WriteRatePct,
    double PeakSlewHzPerSec,
    double MaxElevationDeg,
    double LeadActivePct);
