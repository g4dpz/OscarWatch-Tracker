using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using OscarWatch.Localization;
using OscarWatch.ViewModels;

namespace OscarWatch.Controls;

public sealed class DopplerTimeSeriesChartControl : ThemeAwareControl
{
    private static readonly Color PrimaryRxColor = Color.Parse("#1D4ED8");
    private static readonly Color PrimaryTxColor = Color.Parse("#C2410C");
    private static readonly Color PrimaryThresholdColor = Color.Parse("#0F766E");
    private static readonly Color PrimaryBaseThresholdColor = Color.Parse("#5EEAD4");
    private static readonly Color PrimaryWriteColor = Color.Parse("#7E22CE");
    private static readonly Color ElevationColor = Color.Parse("#64748B");
    private static readonly Color CompareRxColor = Color.Parse("#60A5FA");
    private static readonly Color CompareTxColor = Color.Parse("#FB923C");
    private static readonly Color CompareThresholdColor = Color.Parse("#34D399");
    private static readonly Color CompareWriteColor = Color.Parse("#C084FC");
    private static readonly Color TcaMarkerColor = Color.Parse("#F59E0B");

    private readonly RenderResourceCache _renderCache = new();
    private readonly FormattedTextCache _textCache = new();
    private double _zoomLevel = 1.0;
    private double _panOffsetSeconds = 0.0;

    // Cached LINQ-computed metrics (recomputed only when samples change)
    private double _cachedMaxSeconds;
    private double _cachedMaxHz;
    private double _cachedMaxElev;
    private int _cachedTcaIndex = -1;

    // Cached visible-window filtered samples (recomputed on zoom/pan/sample change)
    private List<DopplerInsightChartSample>? _visiblePrimary;
    private List<DopplerInsightChartSample>? _visibleCompare;
    private double _lastZoom;
    private double _lastPan;

    public static readonly StyledProperty<IReadOnlyList<DopplerInsightChartSample>?> PrimarySamplesProperty =
        AvaloniaProperty.Register<DopplerTimeSeriesChartControl, IReadOnlyList<DopplerInsightChartSample>?>(nameof(PrimarySamples));

    public static readonly StyledProperty<IReadOnlyList<DopplerInsightChartSample>?> ComparisonSamplesProperty =
        AvaloniaProperty.Register<DopplerTimeSeriesChartControl, IReadOnlyList<DopplerInsightChartSample>?>(nameof(ComparisonSamples));

    static DopplerTimeSeriesChartControl()
    {
        AffectsRender<DopplerTimeSeriesChartControl>(PrimarySamplesProperty, ComparisonSamplesProperty);
    }

    public DopplerTimeSeriesChartControl()
    {
        PointerWheelChanged += OnPointerWheelChanged;
    }

    public IReadOnlyList<DopplerInsightChartSample>? PrimarySamples
    {
        get => GetValue(PrimarySamplesProperty);
        set => SetValue(PrimarySamplesProperty, value);
    }

    public IReadOnlyList<DopplerInsightChartSample>? ComparisonSamples
    {
        get => GetValue(ComparisonSamplesProperty);
        set => SetValue(ComparisonSamplesProperty, value);
    }

    public double ZoomLevel
    {
        get => _zoomLevel;
        set
        {
            _zoomLevel = Math.Max(1.0, Math.Min(16.0, value));
            InvalidateVisibleWindow();
            InvalidateVisual();
        }
    }

    public double PanOffsetSeconds
    {
        get => _panOffsetSeconds;
        set
        {
            _panOffsetSeconds = Math.Max(0, value);
            InvalidateVisibleWindow();
            InvalidateVisual();
        }
    }

    public void ResetView()
    {
        _zoomLevel = 1.0;
        _panOffsetSeconds = 0.0;
        InvalidateVisibleWindow();
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PrimarySamplesProperty || change.Property == ComparisonSamplesProperty)
        {
            RecomputeCachedMetrics();
            InvalidateVisibleWindow();
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            ZoomLevel += e.Delta.Y * 0.5;
            e.Handled = true;
        }
    }

    /// <summary>
    /// Recomputes cached metrics from samples. Called when PrimarySamples or ComparisonSamples changes.
    /// </summary>
    internal void RecomputeCachedMetrics()
    {
        var primary = PrimarySamples ?? [];
        var compare = ComparisonSamples ?? [];

        _cachedMaxSeconds = ComputeMaxDurationSeconds(primary, compare);
        _cachedMaxHz = ComputeMaxY(primary, compare);
        _cachedMaxElev = ComputeMaxElevation(primary, compare);
        _cachedTcaIndex = ComputeTcaIndex(primary);
    }

    /// <summary>
    /// Invalidates the visible-window cached sample lists, forcing recomputation on next render.
    /// </summary>
    private void InvalidateVisibleWindow()
    {
        _visiblePrimary = null;
        _visibleCompare = null;
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0)
            return;

        var palette = UiPaletteResolver.Current;
        context.FillRectangle(_renderCache.GetBrush(palette.SkyPlotBackground), new Rect(0, 0, w, h));

        var primary = PrimarySamples ?? [];
        var compare = ComparisonSamples ?? [];
        if (primary.Count == 0 && compare.Count == 0)
        {
            DrawMessage(context, LocalizationService.Instance.Get("DopplerInsights.Chart.Empty"), palette);
            return;
        }

        const double elevStripHeight = 28;
        var plot = new Rect(54, 18, Math.Max(20, w - 66), Math.Max(20, h - elevStripHeight - 48));
        var elevStrip = new Rect(plot.X, plot.Bottom + 10, plot.Width, elevStripHeight);

        var maxSeconds = _cachedMaxSeconds;
        var maxHz = _cachedMaxHz;
        var maxElev = _cachedMaxElev;
        if (maxSeconds <= 0 || maxHz <= 0)
        {
            DrawMessage(context, LocalizationService.Instance.Get("DopplerInsights.Chart.Empty"), palette);
            return;
        }

        var viewportWidthSeconds = maxSeconds / _zoomLevel;
        var clampedPan = Math.Min(_panOffsetSeconds, Math.Max(0, maxSeconds - viewportWidthSeconds));
        var viewStartSeconds = clampedPan;
        var viewEndSeconds = viewStartSeconds + viewportWidthSeconds;

        // Ensure visible-window cache is up to date
        EnsureVisibleWindow(primary, compare, viewStartSeconds, viewEndSeconds);

        DrawGrid(context, plot, palette);
        DrawAxes(context, plot, palette, viewStartSeconds, viewEndSeconds, maxHz);
        DrawThresholdBand(context, _visiblePrimary!, plot, viewStartSeconds, viewEndSeconds, maxHz);

        DrawSeries(context, primary, plot, viewStartSeconds, viewEndSeconds, maxHz, s => s.AbsRxDeltaHz, _renderCache.GetPen(PrimaryRxColor, 2.0), breakOnWrite: true);
        DrawSeries(context, primary, plot, viewStartSeconds, viewEndSeconds, maxHz, s => s.AbsTxDeltaHz, _renderCache.GetPen(PrimaryTxColor, 1.6), breakOnWrite: true);
        DrawWriteTicks(context, primary, plot, viewStartSeconds, viewEndSeconds, _renderCache.GetPen(PrimaryWriteColor, 1.2), 0);

        if (compare.Count > 0)
        {
            DrawSeries(context, compare, plot, viewStartSeconds, viewEndSeconds, maxHz, s => s.AbsRxDeltaHz, _renderCache.GetDashedPen(CompareRxColor, 1.3), breakOnWrite: true);
            DrawSeries(context, compare, plot, viewStartSeconds, viewEndSeconds, maxHz, s => s.AbsTxDeltaHz, _renderCache.GetDashedPen(CompareTxColor, 1.2), breakOnWrite: true);
            DrawWriteTicks(context, compare, plot, viewStartSeconds, viewEndSeconds, _renderCache.GetPen(CompareWriteColor, 1.0), 3);
        }

        if (maxElev > 0)
        {
            DrawElevationStrip(context, primary, elevStrip, viewStartSeconds, viewEndSeconds, maxElev, palette);
            DrawTcaMarker(context, primary, plot, elevStrip, viewStartSeconds, viewEndSeconds, palette, _renderCache, _textCache);
        }
    }

    private void EnsureVisibleWindow(
        IReadOnlyList<DopplerInsightChartSample> primary,
        IReadOnlyList<DopplerInsightChartSample> compare,
        double viewStartSeconds,
        double viewEndSeconds)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (_visiblePrimary is not null && _lastZoom == _zoomLevel && _lastPan == _panOffsetSeconds)
            return;

        _visiblePrimary = FilterToWindow(primary, viewStartSeconds, viewEndSeconds);
        _visibleCompare = FilterToWindow(compare, viewStartSeconds, viewEndSeconds);
        _lastZoom = _zoomLevel;
        _lastPan = _panOffsetSeconds;
    }

    internal static List<DopplerInsightChartSample> FilterToWindow(
        IReadOnlyList<DopplerInsightChartSample> samples,
        double viewStartSeconds,
        double viewEndSeconds)
    {
        var result = new List<DopplerInsightChartSample>();
        for (var i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            if (s.SecondsFromStart >= viewStartSeconds && s.SecondsFromStart <= viewEndSeconds)
                result.Add(s);
        }

        return result;
    }

    internal static double ComputeMaxDurationSeconds(IReadOnlyList<DopplerInsightChartSample> primary, IReadOnlyList<DopplerInsightChartSample> compare)
    {
        var p = primary.Count == 0 ? 0 : primary.Max(s => s.SecondsFromStart);
        var c = compare.Count == 0 ? 0 : compare.Max(s => s.SecondsFromStart);
        return Math.Max(1, Math.Max(p, c));
    }

    internal static double ComputeMaxY(IReadOnlyList<DopplerInsightChartSample> primary, IReadOnlyList<DopplerInsightChartSample> compare)
    {
        static IEnumerable<double> Values(IReadOnlyList<DopplerInsightChartSample> samples)
        {
            foreach (var s in samples)
            {
                yield return s.AbsRxDeltaHz;
                yield return s.AbsTxDeltaHz;
                yield return s.EffectiveThresholdHz;
                yield return s.BaseThresholdHz;
            }
        }

        var max = Values(primary).Concat(Values(compare)).DefaultIfEmpty(0).Max();
        return max <= 0 ? 0 : max * 1.08;
    }

    internal static double ComputeMaxElevation(IReadOnlyList<DopplerInsightChartSample> primary, IReadOnlyList<DopplerInsightChartSample> compare)
    {
        var p = primary.Count == 0 ? 0 : primary.Max(s => s.ElevationDeg);
        var c = compare.Count == 0 ? 0 : compare.Max(s => s.ElevationDeg);
        return Math.Max(p, c);
    }

    internal static int ComputeTcaIndex(IReadOnlyList<DopplerInsightChartSample> samples)
    {
        if (samples.Count == 0)
            return -1;

        var maxIndex = 0;
        var maxElev = samples[0].ElevationDeg;
        for (var i = 1; i < samples.Count; i++)
        {
            if (samples[i].ElevationDeg > maxElev)
            {
                maxElev = samples[i].ElevationDeg;
                maxIndex = i;
            }
        }

        return maxIndex;
    }

    private void DrawGrid(DrawingContext context, Rect plot, UiPalette palette)
    {
        context.DrawRectangle(null, _renderCache.GetPen(palette.SkyPlotBorder, 1), plot);

        var gridPen = _renderCache.GetPen(palette.SkyPlotRing30, 1);
        for (var i = 1; i < 5; i++)
        {
            var x = plot.X + i * plot.Width / 5.0;
            context.DrawLine(gridPen, new Point(x, plot.Y), new Point(x, plot.Bottom));
        }

        for (var i = 1; i < 4; i++)
        {
            var y = plot.Y + i * plot.Height / 4.0;
            context.DrawLine(gridPen, new Point(plot.X, y), new Point(plot.Right, y));
        }
    }

    private void DrawThresholdBand(
        DrawingContext context,
        IReadOnlyList<DopplerInsightChartSample> inView,
        Rect plot,
        double viewStartSeconds,
        double viewEndSeconds,
        double maxHz)
    {
        if (inView.Count == 0)
            return;

        var baseThreshold = 0.0;
        var effectiveSum = 0.0;
        for (var i = 0; i < inView.Count; i++)
        {
            var s = inView[i];
            if (s.BaseThresholdHz > baseThreshold)
                baseThreshold = s.BaseThresholdHz;
            effectiveSum += s.EffectiveThresholdHz;
        }

        if (baseThreshold <= 0)
            return;

        var effectiveThreshold = effectiveSum / inView.Count;
        var baseY = plot.Bottom - plot.Height * (Math.Clamp(baseThreshold, 0, maxHz) / maxHz);
        var effectiveY = plot.Bottom - plot.Height * (Math.Clamp(effectiveThreshold, 0, maxHz) / maxHz);

        var bandBrush = _renderCache.GetBrush(Color.FromArgb(28, PrimaryThresholdColor.R, PrimaryThresholdColor.G, PrimaryThresholdColor.B));
        var top = Math.Min(baseY, effectiveY);
        var bottom = Math.Max(baseY, effectiveY);
        if (bottom - top > 1)
            context.FillRectangle(bandBrush, new Rect(plot.X, top, plot.Width, bottom - top));

        var basePen = _renderCache.GetDashedPen(PrimaryBaseThresholdColor, 1);
        context.DrawLine(basePen, new Point(plot.X, baseY), new Point(plot.Right, baseY));
    }

    private static void DrawSeries(
        DrawingContext context,
        IReadOnlyList<DopplerInsightChartSample> samples,
        Rect plot,
        double viewStartSeconds,
        double viewEndSeconds,
        double maxHz,
        Func<DopplerInsightChartSample, double> selector,
        Pen pen,
        bool breakOnWrite)
    {
        if (samples.Count < 2)
            return;

        var viewportWidth = viewEndSeconds - viewStartSeconds;
        if (viewportWidth <= 0)
            return;

        Point? previous = null;
        foreach (var sample in samples)
        {
            if (sample.SecondsFromStart < viewStartSeconds || sample.SecondsFromStart > viewEndSeconds)
            {
                previous = null;
                continue;
            }

            if (breakOnWrite && previous is not null && (sample.WroteRx || sample.WroteTx))
                previous = null;

            var relativeTime = sample.SecondsFromStart - viewStartSeconds;
            var x = plot.X + plot.Width * (relativeTime / viewportWidth);
            var yValue = Math.Clamp(selector(sample), 0, maxHz);
            var y = plot.Bottom - plot.Height * (yValue / maxHz);
            var current = new Point(x, y);

            if (previous is { } p)
                context.DrawLine(pen, p, current);

            previous = current;
        }
    }

    private static void DrawWriteTicks(
        DrawingContext context,
        IReadOnlyList<DopplerInsightChartSample> samples,
        Rect plot,
        double viewStartSeconds,
        double viewEndSeconds,
        Pen pen,
        double yOffset)
    {
        var viewportWidth = viewEndSeconds - viewStartSeconds;
        if (viewportWidth <= 0)
            return;

        foreach (var sample in samples)
        {
            if (!sample.WroteRx && !sample.WroteTx)
                continue;

            if (sample.SecondsFromStart < viewStartSeconds || sample.SecondsFromStart > viewEndSeconds)
                continue;

            var relativeTime = sample.SecondsFromStart - viewStartSeconds;
            var x = plot.X + plot.Width * (relativeTime / viewportWidth);
            var y1 = plot.Bottom - yOffset;
            var y2 = plot.Y + 4;
            context.DrawLine(pen, new Point(x, y1), new Point(x, y2));
        }
    }

    private void DrawElevationStrip(
        DrawingContext context,
        IReadOnlyList<DopplerInsightChartSample> samples,
        Rect strip,
        double viewStartSeconds,
        double viewEndSeconds,
        double maxElev,
        UiPalette palette)
    {
        context.DrawRectangle(null, _renderCache.GetPen(palette.SkyPlotBorder, 1), strip);

        var viewportWidth = viewEndSeconds - viewStartSeconds;
        if (viewportWidth <= 0)
            return;

        var elevPen = _renderCache.GetPen(ElevationColor, 1.5);
        Point? previous = null;
        foreach (var sample in samples)
        {
            if (sample.SecondsFromStart < viewStartSeconds || sample.SecondsFromStart > viewEndSeconds)
            {
                previous = null;
                continue;
            }

            var relativeTime = sample.SecondsFromStart - viewStartSeconds;
            var x = strip.X + strip.Width * (relativeTime / viewportWidth);
            var y = strip.Bottom - strip.Height * (Math.Clamp(sample.ElevationDeg, 0, maxElev) / maxElev);
            var current = new Point(x, y);

            if (previous is { } p)
                context.DrawLine(elevPen, p, current);

            previous = current;
        }

        var label = LocalizationService.Instance.Get("DopplerInsights.Chart.ElevationLabel");
        var formatted = _textCache.Get(label, 9, palette);
        context.DrawText(formatted, new Point(strip.X - 52, strip.Y + 6));
    }

    private static void DrawTcaMarker(
        DrawingContext context,
        IReadOnlyList<DopplerInsightChartSample> samples,
        Rect plot,
        Rect elevStrip,
        double viewStartSeconds,
        double viewEndSeconds,
        UiPalette palette,
        RenderResourceCache renderCache,
        FormattedTextCache textCache)
    {
        if (samples.Count == 0)
            return;

        var tca = samples.OrderByDescending(s => s.ElevationDeg).First();
        if (tca.ElevationDeg <= 0 || tca.SecondsFromStart < viewStartSeconds || tca.SecondsFromStart > viewEndSeconds)
            return;

        var viewportWidth = viewEndSeconds - viewStartSeconds;
        var relativeTime = tca.SecondsFromStart - viewStartSeconds;
        var x = plot.X + plot.Width * (relativeTime / viewportWidth);

        var markerPen = renderCache.GetDashedPen(TcaMarkerColor, 1);
        context.DrawLine(markerPen, new Point(x, plot.Y), new Point(x, elevStrip.Bottom));

        var label = LocalizationService.Instance.Get("DopplerInsights.Chart.TcaLabel");
        var formatted = textCache.Get(label, 9, palette);
        context.DrawText(formatted, new Point(Math.Min(x + 3, plot.Right - formatted.Width), plot.Y + 2));
    }

    private void DrawMessage(DrawingContext context, string text, UiPalette palette)
    {
        var formatted = _textCache.Get(text, 12, palette);
        context.DrawText(formatted, new Point(14, 14));
    }

    private void DrawAxes(DrawingContext context, Rect plot, UiPalette palette, double viewStartSeconds, double viewEndSeconds, double maxHz)
    {
        var labelBrush = _renderCache.GetBrush(palette.SkyPlotLabel);
        var yTitle = LocalizationService.Instance.Get("DopplerInsights.Chart.YAxis");
        var yLabel = _textCache.Get(yTitle, 10, palette);
        context.DrawText(yLabel, new Point(4, plot.Y + plot.Height / 2 - yLabel.Height / 2));

        var yMaxText = $"{Math.Round(maxHz)} Hz";
        var yMax = _textCache.Get(yMaxText, 9, palette);
        context.DrawText(yMax, new Point(4, plot.Y - 2));

        var leftText = FormatPassTime(viewStartSeconds);
        var left = _textCache.Get(leftText, 10, palette);
        context.DrawText(left, new Point(plot.X, plot.Bottom + 3));

        var rightText = FormatPassTime(viewEndSeconds);
        var right = _textCache.Get(rightText, 10, palette);
        context.DrawText(right, new Point(Math.Max(plot.X, plot.Right - right.Width), plot.Bottom + 3));
    }

    private static string FormatPassTime(double seconds)
    {
        if (seconds < 120)
            return $"{Math.Round(seconds)}s";

        var minutes = (int)(seconds / 60);
        var remainder = (int)Math.Round(seconds % 60);
        return $"{minutes}m {remainder}s";
    }

    // Expose cached metrics for testing
    internal double CachedMaxSeconds => _cachedMaxSeconds;
    internal double CachedMaxHz => _cachedMaxHz;
    internal double CachedMaxElev => _cachedMaxElev;
    internal int CachedTcaIndex => _cachedTcaIndex;
}
