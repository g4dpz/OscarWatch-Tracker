using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OscarWatch.Core.Display;
using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Core.Services;
using OscarWatch.Localization;

namespace OscarWatch.ViewModels;

public partial class SunlightPredictionViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly ITleService _tleService;
    private readonly IIlluminationPredictor _predictor;
    private readonly ILocalizationService _l;
    private CancellationTokenSource? _computeCts;
    private IReadOnlyList<IlluminationSegment> _lastSegments = [];

    public ObservableCollection<SatelliteCatalogEntry> Satellites { get; } = [];
    public ObservableCollection<SunlightMonthTimelineRow> MonthTimelines { get; } = [];
    public ObservableCollection<SunlightPeriodRow> SunlightPeriods { get; } = [];

    [ObservableProperty]
    private SatelliteCatalogEntry? _selectedSatellite;

    [ObservableProperty]
    private DateTimeOffset _startDate = DateTimeOffset.UtcNow.Date;

    [ObservableProperty]
    private int _predictionDays = 365;

    [ObservableProperty]
    private int _minSunlightMinutes;

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private string _summaryText = "";

    [ObservableProperty]
    private bool _hasResults;

    [ObservableProperty]
    private bool _isComputing;

    public SunlightPredictionViewModel(
        ISettingsService settings,
        ITleService tleService,
        IIlluminationPredictor predictor,
        ILocalizationService localization)
    {
        _settings = settings;
        _tleService = tleService;
        _predictor = predictor;
        _l = localization;
        _statusText = _l.Get("Sunlight.Status.SelectPredict");
    }

    public async Task InitializeAsync()
    {
        await _tleService.EnsureLoadedAsync();
        Satellites.Clear();
        foreach (var sat in _tleService.GetEnabledSatellites(_settings.Current))
            Satellites.Add(sat);

        SelectedSatellite ??= Satellites.FirstOrDefault();
        if (Satellites.Count == 0)
            StatusText = _l.Get("Sunlight.Status.NoSatellites");
    }

    [RelayCommand(CanExecute = nameof(CanPredict))]
    private async Task PredictAsync()
    {
        if (SelectedSatellite is not { } satellite)
            return;

        _computeCts?.Cancel();
        _computeCts = new CancellationTokenSource();
        var token = _computeCts.Token;

        IsComputing = true;
        HasResults = false;
        MonthTimelines.Clear();
        SunlightPeriods.Clear();
        SummaryText = "";
        StatusText = _l.Get("Sunlight.Status.Computing", satellite.Name);

        try
        {
            var utcStart = StartDate.UtcDateTime;
            var duration = TimeSpan.FromDays(Math.Clamp(PredictionDays, 1, 366));
            var utcEnd = utcStart + duration;

            var segments = await _predictor.PredictAsync(satellite, utcStart, duration, token);
            if (token.IsCancellationRequested)
                return;

            _lastSegments = segments;
            BuildMonthTimelines(segments, utcStart, utcEnd);
            BuildSunlightPeriods(segments);
            BuildSummary(segments, utcStart, utcEnd);

            HasResults = true;
            StatusText = _l.Get("Sunlight.Status.Segments", segments.Count, PredictionDays);
        }
        catch (OperationCanceledException)
        {
            StatusText = _l.Get("Sunlight.Status.Cancelled");
        }
        catch (Exception ex)
        {
            StatusText = _l.Get("Sunlight.Status.Failed", ex.Message);
        }
        finally
        {
            IsComputing = false;
        }
    }

    private bool CanPredict() => SelectedSatellite is not null && !IsComputing;

    partial void OnSelectedSatelliteChanged(SatelliteCatalogEntry? value) =>
        PredictCommand.NotifyCanExecuteChanged();

    partial void OnIsComputingChanged(bool value) =>
        PredictCommand.NotifyCanExecuteChanged();

    partial void OnMinSunlightMinutesChanged(int value)
    {
        if (!HasResults || _lastSegments.Count == 0)
            return;

        BuildSunlightPeriods(_lastSegments);
    }

    private void BuildMonthTimelines(
        IReadOnlyList<IlluminationSegment> segments,
        DateTime utcStart,
        DateTime utcEnd)
    {
        MonthTimelines.Clear();
        var monthCursor = new DateTime(utcStart.Year, utcStart.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        while (monthCursor < utcEnd)
        {
            var monthEnd = monthCursor.AddMonths(1);
            var rangeStart = monthCursor < utcStart ? utcStart : monthCursor;
            var rangeEnd = monthEnd > utcEnd ? utcEnd : monthEnd;
            if (rangeEnd > rangeStart)
            {
                MonthTimelines.Add(new SunlightMonthTimelineRow
                {
                    MonthLabel = PassDisplayFormat.FormatMonthYear(rangeStart),
                    Segments = ClipSegments(segments, rangeStart, rangeEnd),
                    RangeStartUtc = rangeStart,
                    RangeEndUtc = rangeEnd
                });
            }

            monthCursor = monthEnd;
        }
    }

    private void BuildSunlightPeriods(IReadOnlyList<IlluminationSegment> segments)
    {
        SunlightPeriods.Clear();
        var minDuration = TimeSpan.FromMinutes(Math.Max(0, MinSunlightMinutes));

        foreach (var segment in segments.Where(s => s.IsSunlit && s.Duration >= minDuration))
        {
            SunlightPeriods.Add(new SunlightPeriodRow
            {
                StartLocal = PassDisplayFormat.FormatLocal(segment.StartUtc),
                EndLocal = PassDisplayFormat.FormatLocal(segment.EndUtc),
                Duration = PassDisplayFormat.FormatDurationLong(segment.Duration),
                DurationSort = segment.Duration
            });
        }

        var ordered = SunlightPeriods.OrderByDescending(r => r.DurationSort).ToList();
        SunlightPeriods.Clear();
        foreach (var row in ordered)
            SunlightPeriods.Add(row);
    }

    private void BuildSummary(
        IReadOnlyList<IlluminationSegment> segments,
        DateTime utcStart,
        DateTime utcEnd)
    {
        var totalSpan = utcEnd - utcStart;
        var sunlit = segments.Where(s => s.IsSunlit).Sum(s => s.Duration.TotalSeconds);
        var sunlitPercent = totalSpan.TotalSeconds > 0 ? sunlit / totalSpan.TotalSeconds * 100 : 0;

        var longestSun = segments.Where(s => s.IsSunlit).MaxBy(s => s.Duration);
        var longestEclipse = segments.Where(s => !s.IsSunlit).MaxBy(s => s.Duration);

        var parts = new List<string>
        {
            _l.Get("Sunlight.Summary.SunlitPercent", $"{sunlitPercent:F0}")
        };

        if (longestSun is not null)
        {
            parts.Add(_l.Get(
                "Sunlight.Summary.LongestSun",
                PassDisplayFormat.FormatDurationLong(longestSun.Duration),
                PassDisplayFormat.FormatLocal(longestSun.StartUtc),
                PassDisplayFormat.FormatLocal(longestSun.EndUtc)));
        }

        if (longestEclipse is not null)
        {
            parts.Add(_l.Get(
                "Sunlight.Summary.LongestEclipse",
                PassDisplayFormat.FormatDurationLong(longestEclipse.Duration)));
        }

        SummaryText = string.Join(" · ", parts);
    }

    private static List<IlluminationSegment> ClipSegments(
        IReadOnlyList<IlluminationSegment> segments,
        DateTime rangeStart,
        DateTime rangeEnd)
    {
        var clipped = new List<IlluminationSegment>();
        foreach (var segment in segments)
        {
            var start = segment.StartUtc < rangeStart ? rangeStart : segment.StartUtc;
            var end = segment.EndUtc > rangeEnd ? rangeEnd : segment.EndUtc;
            if (end <= start)
                continue;

            clipped.Add(new IlluminationSegment
            {
                StartUtc = start,
                EndUtc = end,
                IsSunlit = segment.IsSunlit
            });
        }

        return clipped;
    }
}

public sealed class SunlightMonthTimelineRow
{
    public string MonthLabel { get; init; } = "";
    public IReadOnlyList<IlluminationSegment> Segments { get; init; } = [];
    public DateTime RangeStartUtc { get; init; }
    public DateTime RangeEndUtc { get; init; }
}

public sealed class SunlightPeriodRow
{
    public string StartLocal { get; init; } = "";
    public string EndLocal { get; init; } = "";
    public string Duration { get; init; } = "";
    public TimeSpan DurationSort { get; init; }
}
