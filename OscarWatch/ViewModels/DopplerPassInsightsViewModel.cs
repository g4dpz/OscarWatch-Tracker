using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OscarWatch.Core.Display;
using OscarWatch.Localization;

namespace OscarWatch.ViewModels;

public partial class DopplerPassInsightsViewModel : ViewModelBase
{
    private readonly ILocalizationService _l;
    private List<DopplerInsightCsvRow> _primaryRows = [];
    private List<DopplerInsightCsvRow> _comparisonRows = [];

    public DopplerPassInsightsViewModel(ILocalizationService localization)
    {
        _l = localization;
        StatusText = _l.Get("DopplerInsights.Status.Ready");
        PassSummaryText = _l.Get("DopplerInsights.PassSummary.Empty");
        ThresholdSummaryText = _l.Get("DopplerInsights.Threshold.Empty");
        ActivitySummaryText = _l.Get("DopplerInsights.Activity.Empty");
        DynamicsSummaryText = _l.Get("DopplerInsights.Dynamics.Empty");
        ComparisonSummaryText = _l.Get("DopplerInsights.Compare.Empty");
        ComparisonInterpretationTitle = _l.Get("DopplerInsights.Compare.Interpretation.EmptyTitle");
        ComparisonInterpretationText = _l.Get("DopplerInsights.Compare.Interpretation.EmptyText");
    }

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private string _selectedFilePath = "";

    [ObservableProperty]
    private string _comparisonFilePath = "";

    [ObservableProperty]
    private bool _hasComparison;

    [ObservableProperty]
    private string _passSummaryText = "";

    [ObservableProperty]
    private string _thresholdSummaryText = "";

    [ObservableProperty]
    private string _activitySummaryText = "";

    [ObservableProperty]
    private string _dynamicsSummaryText = "";

    [ObservableProperty]
    private string _comparisonSummaryText = "";

    [ObservableProperty]
    private string _comparisonInterpretationTitle = "";

    [ObservableProperty]
    private string _comparisonInterpretationText = "";

    [ObservableProperty]
    private double _rxWritePercent;

    [ObservableProperty]
    private double _txWritePercent;

    [ObservableProperty]
    private double _belowThresholdPercent;

    [ObservableProperty]
    private double _interactivePercent;

    public ObservableCollection<string> Recommendations { get; } = [];

    public ObservableCollection<DopplerInsightEventRow> EventRows { get; } = [];

    public ObservableCollection<DopplerInsightChartSample> PrimaryChartSamples { get; } = [];

    public ObservableCollection<DopplerInsightChartSample> ComparisonChartSamples { get; } = [];

    [RelayCommand]
    private async Task LoadLatestAsync()
    {
        try
        {
            var directory = DopplerPassLogFileNameFormat.GetDefaultLogDirectory();
            if (!Directory.Exists(directory))
            {
                StatusText = _l.Get("DopplerInsights.Status.NoLogsFolder", directory);
                return;
            }

            var latest = Directory.GetFiles(directory, "*.csv", SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                .FirstOrDefault();

            if (latest is null)
            {
                StatusText = _l.Get("DopplerInsights.Status.NoCsvLogs", directory);
                return;
            }

            await LoadFromFileAsync(latest.FullName).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusText = _l.Get("DopplerInsights.Status.LoadFailed", ex.Message);
        }
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        try
        {
            DopplerPassLogFileNameFormat.OpenLogDirectory(null);
            StatusText = _l.Get("DopplerInsights.Status.OpenFolderOk");
        }
        catch (Exception ex)
        {
            StatusText = _l.Get("DopplerInsights.Status.OpenFolderFailed", ex.Message);
        }
    }

    [RelayCommand]
    private void ClearComparison()
    {
        _comparisonRows = [];
        ComparisonChartSamples.Clear();
        ComparisonFilePath = "";
        HasComparison = false;
        ComparisonSummaryText = _l.Get("DopplerInsights.Compare.Empty");
        ComparisonInterpretationTitle = _l.Get("DopplerInsights.Compare.Interpretation.EmptyTitle");
        ComparisonInterpretationText = _l.Get("DopplerInsights.Compare.Interpretation.EmptyText");
    }

    public async Task LoadFromFileAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            StatusText = _l.Get("DopplerInsights.Status.FileMissing");
            return;
        }

        SelectedFilePath = path;
        StatusText = _l.Get("DopplerInsights.Status.Loading");

        try
        {
            var lines = await File.ReadAllLinesAsync(path).ConfigureAwait(true);
            _primaryRows = ParseRows(lines);
            ApplyPrimaryMetrics(_primaryRows);
            RebuildChartSamples();
            UpdateComparisonSummary();
            StatusText = _l.Get("DopplerInsights.Status.Loaded", _primaryRows.Count);
        }
        catch (Exception ex)
        {
            StatusText = _l.Get("DopplerInsights.Status.LoadFailed", ex.Message);
        }
    }

    public async Task LoadComparisonFromFileAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            StatusText = _l.Get("DopplerInsights.Status.FileMissing");
            return;
        }

        ComparisonFilePath = path;
        StatusText = _l.Get("DopplerInsights.Status.LoadingCompare");

        try
        {
            var lines = await File.ReadAllLinesAsync(path).ConfigureAwait(true);
            _comparisonRows = ParseRows(lines);
            HasComparison = _comparisonRows.Count > 0;
            RebuildChartSamples();
            UpdateComparisonSummary();
            StatusText = _l.Get("DopplerInsights.Status.LoadedCompare", _comparisonRows.Count);
        }
        catch (Exception ex)
        {
            StatusText = _l.Get("DopplerInsights.Status.LoadFailed", ex.Message);
        }
    }

    private void ApplyPrimaryMetrics(List<DopplerInsightCsvRow> rows)
    {
        Recommendations.Clear();
        EventRows.Clear();

        if (rows.Count == 0)
        {
            PassSummaryText = _l.Get("DopplerInsights.PassSummary.Empty");
            ThresholdSummaryText = _l.Get("DopplerInsights.Threshold.Empty");
            ActivitySummaryText = _l.Get("DopplerInsights.Activity.Empty");
            DynamicsSummaryText = _l.Get("DopplerInsights.Dynamics.Empty");
            RxWritePercent = 0;
            TxWritePercent = 0;
            BelowThresholdPercent = 0;
            InteractivePercent = 0;
            Recommendations.Add(_l.Get("DopplerInsights.Reco.Empty"));
            return;
        }

        var snapshots = rows.Where(r => string.Equals(r.Event, "snapshot", StringComparison.OrdinalIgnoreCase)).ToList();
        var writeRows = rows.Where(r => string.Equals(r.Event, "cat_write", StringComparison.OrdinalIgnoreCase)).ToList();
        var offsetRows = rows.Where(r => string.Equals(r.Event, "offset_change", StringComparison.OrdinalIgnoreCase)).ToList();

        var total = rows.Count;
        var rxWrites = rows.Count(r => r.WroteRx);
        var txWrites = rows.Count(r => r.WroteTx);
        var belowThresholdCount = rows.Count(r => r.BelowThreshold);
        var interactiveCount = rows.Count(r => r.Interactive);
        var catPausedCount = rows.Count(r => r.CatPaused);

        var effectiveThresholdAvg = AverageOrZero(rows.Select(r => (double)r.EffectiveThresholdHz));
        var baseThresholdAvg = AverageOrZero(rows.Select(r => (double)r.BaseThresholdHz));
        var thresholdReductionPct = baseThresholdAvg <= 0
            ? 0
            : Math.Max(0, (baseThresholdAvg - effectiveThresholdAvg) / baseThresholdAvg * 100.0);

        var rxAbs = rows.Select(r => Math.Abs((double)r.RxDeltaHz)).ToList();
        var txAbs = rows.Select(r => Math.Abs((double)r.TxDeltaHz)).ToList();
        var maxSlew = rows.Select(r => Math.Abs(r.SlewHzPerSec)).DefaultIfEmpty(0).Max();
        var leadEnabledPct = Percent(rows.Count(r => r.LeadEnabled), total);

        RxWritePercent = Percent(rxWrites, total);
        TxWritePercent = Percent(txWrites, total);
        BelowThresholdPercent = Percent(belowThresholdCount, total);
        InteractivePercent = Percent(interactiveCount, total);

        var start = rows.Min(r => r.Utc);
        var end = rows.Max(r => r.Utc);
        var sat = rows.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.SatelliteName))?.SatelliteName ?? "-";
        PassSummaryText = _l.Get(
            "DopplerInsights.PassSummary.Value",
            sat,
            start.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            end.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            Math.Max(0, (end - start).TotalMinutes).ToString("0.0", CultureInfo.InvariantCulture),
            total,
            snapshots.Count,
            writeRows.Count,
            offsetRows.Count);

        ThresholdSummaryText = _l.Get(
            "DopplerInsights.Threshold.Value",
            baseThresholdAvg.ToString("0", CultureInfo.InvariantCulture),
            effectiveThresholdAvg.ToString("0", CultureInfo.InvariantCulture),
            thresholdReductionPct.ToString("0", CultureInfo.InvariantCulture),
            BelowThresholdPercent.ToString("0.0", CultureInfo.InvariantCulture));

        ActivitySummaryText = _l.Get(
            "DopplerInsights.Activity.Value",
            RxWritePercent.ToString("0.0", CultureInfo.InvariantCulture),
            TxWritePercent.ToString("0.0", CultureInfo.InvariantCulture),
            InteractivePercent.ToString("0.0", CultureInfo.InvariantCulture),
            catPausedCount,
            leadEnabledPct.ToString("0.0", CultureInfo.InvariantCulture));

        DynamicsSummaryText = _l.Get(
            "DopplerInsights.Dynamics.Value",
            AverageOrZero(rxAbs).ToString("0", CultureInfo.InvariantCulture),
            P95(rxAbs).ToString("0", CultureInfo.InvariantCulture),
            AverageOrZero(txAbs).ToString("0", CultureInfo.InvariantCulture),
            P95(txAbs).ToString("0", CultureInfo.InvariantCulture),
            maxSlew.ToString("0", CultureInfo.InvariantCulture));

        BuildRecommendations(
            belowThresholdPercent: BelowThresholdPercent,
            rxWritePercent: RxWritePercent,
            txWritePercent: TxWritePercent,
            interactiveCount: interactiveCount,
            catPausedCount: catPausedCount,
            leadEnabledPct: leadEnabledPct,
            maxSlew: maxSlew,
            thresholdReductionPct: thresholdReductionPct);

        foreach (var row in rows.OrderByDescending(r => r.Utc).Take(160))
        {
            EventRows.Add(new DopplerInsightEventRow(
                row.Utc.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                row.Event,
                row.RxDeltaHz,
                row.TxDeltaHz,
                row.WroteRx,
                row.WroteTx,
                row.BelowThreshold,
                row.Notes ?? ""));
        }
    }

    private void RebuildChartSamples()
    {
        PrimaryChartSamples.Clear();
        foreach (var sample in BuildChartSamples(_primaryRows))
            PrimaryChartSamples.Add(sample);

        ComparisonChartSamples.Clear();
        foreach (var sample in BuildChartSamples(_comparisonRows))
            ComparisonChartSamples.Add(sample);
    }

    private void UpdateComparisonSummary()
    {
        if (_primaryRows.Count == 0 || _comparisonRows.Count == 0)
        {
            ComparisonSummaryText = _l.Get("DopplerInsights.Compare.Empty");
            ComparisonInterpretationTitle = _l.Get("DopplerInsights.Compare.Interpretation.EmptyTitle");
            ComparisonInterpretationText = _l.Get("DopplerInsights.Compare.Interpretation.EmptyText");
            HasComparison = _comparisonRows.Count > 0;
            return;
        }

        var primary = Summarize(_primaryRows);
        var compare = Summarize(_comparisonRows);
        ComparisonSummaryText = _l.Get(
            "DopplerInsights.Compare.Value",
            Path.GetFileName(SelectedFilePath),
            primary.RxWritePct.ToString("0.0", CultureInfo.InvariantCulture),
            primary.TxWritePct.ToString("0.0", CultureInfo.InvariantCulture),
            primary.RxP95.ToString("0", CultureInfo.InvariantCulture),
            primary.TxP95.ToString("0", CultureInfo.InvariantCulture),
            Path.GetFileName(ComparisonFilePath),
            compare.RxWritePct.ToString("0.0", CultureInfo.InvariantCulture),
            compare.TxWritePct.ToString("0.0", CultureInfo.InvariantCulture),
            compare.RxP95.ToString("0", CultureInfo.InvariantCulture),
            compare.TxP95.ToString("0", CultureInfo.InvariantCulture));

        var rxWriteDelta = compare.RxWritePct - primary.RxWritePct;
        var txWriteDelta = compare.TxWritePct - primary.TxWritePct;
        var rxP95Delta = compare.RxP95 - primary.RxP95;
        var txP95Delta = compare.TxP95 - primary.TxP95;

        var score = 0;
        if (rxP95Delta < -8)
            score++;
        if (txP95Delta < -8)
            score++;
        if (Math.Abs(rxWriteDelta) <= 8)
            score++;
        if (Math.Abs(txWriteDelta) <= 8)
            score++;

        ComparisonInterpretationTitle = score switch
        {
            >= 3 => _l.Get("DopplerInsights.Compare.Interpretation.BetterTitle"),
            <= 1 => _l.Get("DopplerInsights.Compare.Interpretation.WorseTitle"),
            _ => _l.Get("DopplerInsights.Compare.Interpretation.MixedTitle")
        };

        var action = ResolveComparisonAction(rxWriteDelta, txWriteDelta, rxP95Delta, txP95Delta);
        ComparisonInterpretationText = _l.Get(
            "DopplerInsights.Compare.Interpretation.Value",
            Signed(rxWriteDelta),
            Signed(txWriteDelta),
            Signed(rxP95Delta),
            Signed(txP95Delta),
            action);
    }

    private string ResolveComparisonAction(double rxWriteDelta, double txWriteDelta, double rxP95Delta, double txP95Delta)
    {
        if (rxP95Delta > 10 || txP95Delta > 10)
            return _l.Get("DopplerInsights.Compare.Action.IncreaseAggression");

        if (Math.Abs(rxWriteDelta) > 12 || Math.Abs(txWriteDelta) > 12)
            return _l.Get("DopplerInsights.Compare.Action.StabilizeWrites");

        if (rxP95Delta < -10 && txP95Delta < -10)
            return _l.Get("DopplerInsights.Compare.Action.KeepSettings");

        return _l.Get("DopplerInsights.Compare.Action.SmallStep");
    }

    private static string Signed(double value)
    {
        var prefix = value >= 0 ? "+" : "";
        return prefix + value.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private static DopplerAggregateSummary Summarize(IReadOnlyList<DopplerInsightCsvRow> rows)
    {
        if (rows.Count == 0)
            return new DopplerAggregateSummary(0, 0, 0, 0);

        var total = rows.Count;
        var rxWritePct = Percent(rows.Count(r => r.WroteRx), total);
        var txWritePct = Percent(rows.Count(r => r.WroteTx), total);
        var rxP95 = P95(rows.Select(r => Math.Abs((double)r.RxDeltaHz)));
        var txP95 = P95(rows.Select(r => Math.Abs((double)r.TxDeltaHz)));
        return new DopplerAggregateSummary(rxWritePct, txWritePct, rxP95, txP95);
    }

    private static IReadOnlyList<DopplerInsightChartSample> BuildChartSamples(IReadOnlyList<DopplerInsightCsvRow> rows)
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
        return sorted.Select(r => new DopplerInsightChartSample(
            SecondsFromStart: Math.Max(0, (r.Utc - startUtc).TotalSeconds),
            AbsRxDeltaHz: Math.Abs(r.RxDeltaHz),
            AbsTxDeltaHz: Math.Abs(r.TxDeltaHz),
            EffectiveThresholdHz: Math.Max(0, r.EffectiveThresholdHz),
            SlewHzPerSec: Math.Abs(r.SlewHzPerSec),
            WroteRx: r.WroteRx,
            WroteTx: r.WroteTx,
            BelowThreshold: r.BelowThreshold))
            .ToArray();
    }

    private void BuildRecommendations(
        double belowThresholdPercent,
        double rxWritePercent,
        double txWritePercent,
        int interactiveCount,
        int catPausedCount,
        double leadEnabledPct,
        double maxSlew,
        double thresholdReductionPct)
    {
        if (belowThresholdPercent >= 75 && rxWritePercent < 12)
            Recommendations.Add(_l.Get("DopplerInsights.Reco.ThresholdHigh"));

        if (rxWritePercent >= 80 || txWritePercent >= 80)
            Recommendations.Add(_l.Get("DopplerInsights.Reco.ThresholdLow"));

        if (interactiveCount > 0)
            Recommendations.Add(_l.Get("DopplerInsights.Reco.Interactive"));

        if (catPausedCount > 0)
            Recommendations.Add(_l.Get("DopplerInsights.Reco.CatPaused"));

        if (maxSlew >= 250 && leadEnabledPct < 40)
            Recommendations.Add(_l.Get("DopplerInsights.Reco.EnableLead"));

        if (thresholdReductionPct > 20)
            Recommendations.Add(_l.Get("DopplerInsights.Reco.AdaptiveThreshold"));

        if (Recommendations.Count == 0)
            Recommendations.Add(_l.Get("DopplerInsights.Reco.Balanced"));
    }

    private static List<DopplerInsightCsvRow> ParseRows(IReadOnlyList<string> lines)
    {
        var rows = new List<DopplerInsightCsvRow>();
        string[]? header = null;
        Dictionary<string, int>? index = null;

        foreach (var raw in lines)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            if (raw.StartsWith('#'))
                continue;

            var columns = ParseCsvLine(raw);
            if (header is null)
            {
                header = columns.ToArray();
                index = BuildHeaderIndex(header);
                continue;
            }

            if (index is null)
                continue;

            rows.Add(new DopplerInsightCsvRow(
                Utc: ReadDate(columns, index, "Utc"),
                Event: ReadString(columns, index, "Event"),
                SatelliteName: ReadString(columns, index, "Satellite"),
                BaseThresholdHz: ReadInt(columns, index, "BaseThresholdHz"),
                EffectiveThresholdHz: ReadInt(columns, index, "EffectiveThresholdHz"),
                SlewHzPerSec: ReadDouble(columns, index, "SlewHzPerSec"),
                RxDeltaHz: ReadLong(columns, index, "RxDeltaHz"),
                TxDeltaHz: ReadLong(columns, index, "TxDeltaHz"),
                WroteRx: ReadBool(columns, index, "WroteRx"),
                WroteTx: ReadBool(columns, index, "WroteTx"),
                BelowThreshold: ReadBool(columns, index, "BelowThreshold"),
                Interactive: ReadBool(columns, index, "Interactive"),
                CatPaused: ReadBool(columns, index, "CatPaused"),
                LeadEnabled: ReadBool(columns, index, "LeadEnabled"),
                Notes: ReadString(columns, index, "Notes")));
        }

        return rows;
    }

    private static Dictionary<string, int> BuildHeaderIndex(IReadOnlyList<string> header)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Count; i++)
            map[header[i]] = i;
        return map;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        result.Add(current.ToString());
        return result;
    }

    private static string ReadString(IReadOnlyList<string> columns, IReadOnlyDictionary<string, int> index, string key)
    {
        if (!index.TryGetValue(key, out var i) || i < 0 || i >= columns.Count)
            return "";
        return columns[i].Trim();
    }

    private static int ReadInt(IReadOnlyList<string> columns, IReadOnlyDictionary<string, int> index, string key)
    {
        var text = ReadString(columns, index, key);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static long ReadLong(IReadOnlyList<string> columns, IReadOnlyDictionary<string, int> index, string key)
    {
        var text = ReadString(columns, index, key);
        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static double ReadDouble(IReadOnlyList<string> columns, IReadOnlyDictionary<string, int> index, string key)
    {
        var text = ReadString(columns, index, key);
        return double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static bool ReadBool(IReadOnlyList<string> columns, IReadOnlyDictionary<string, int> index, string key)
    {
        var text = ReadString(columns, index, key);
        if (string.Equals(text, "1", StringComparison.Ordinal) || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(text, "0", StringComparison.Ordinal) || string.Equals(text, "false", StringComparison.OrdinalIgnoreCase))
            return false;
        return false;
    }

    private static DateTime ReadDate(IReadOnlyList<string> columns, IReadOnlyDictionary<string, int> index, string key)
    {
        var text = ReadString(columns, index, key);
        if (DateTime.TryParseExact(text, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var precise))
            return precise;

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var fallback))
            return fallback;

        return DateTime.MinValue;
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

public sealed record DopplerInsightEventRow(
    string Utc,
    string Event,
    long RxDeltaHz,
    long TxDeltaHz,
    bool WroteRx,
    bool WroteTx,
    bool BelowThreshold,
    string Notes);

public sealed record DopplerInsightChartSample(
    double SecondsFromStart,
    long AbsRxDeltaHz,
    long AbsTxDeltaHz,
    int EffectiveThresholdHz,
    double SlewHzPerSec,
    bool WroteRx,
    bool WroteTx,
    bool BelowThreshold);

internal sealed record DopplerInsightCsvRow(
    DateTime Utc,
    string Event,
    string SatelliteName,
    int BaseThresholdHz,
    int EffectiveThresholdHz,
    double SlewHzPerSec,
    long RxDeltaHz,
    long TxDeltaHz,
    bool WroteRx,
    bool WroteTx,
    bool BelowThreshold,
    bool Interactive,
    bool CatPaused,
    bool LeadEnabled,
    string Notes);

internal sealed record DopplerAggregateSummary(
    double RxWritePct,
    double TxWritePct,
    double RxP95,
    double TxP95);
