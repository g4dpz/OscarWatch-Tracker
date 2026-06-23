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
    private static readonly Color PrimaryWriteColor = Color.Parse("#7E22CE");
    private static readonly Color CompareRxColor = Color.Parse("#60A5FA");
    private static readonly Color CompareTxColor = Color.Parse("#FB923C");
    private static readonly Color CompareThresholdColor = Color.Parse("#34D399");
    private static readonly Color CompareWriteColor = Color.Parse("#C084FC");

    private readonly RenderResourceCache _renderCache = new();
    private double _zoomLevel = 1.0;
    private double _panOffsetSeconds = 0.0;

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
            InvalidateVisual();
        }
    }

    public double PanOffsetSeconds
    {
        get => _panOffsetSeconds;
        set
        {
            _panOffsetSeconds = Math.Max(0, value);
            InvalidateVisual();
        }
    }

    public void ResetView()
    {
        _zoomLevel = 1.0;
        _panOffsetSeconds = 0.0;
        InvalidateVisual();
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            ZoomLevel += e.Delta.Y * 0.5;
            e.Handled = true;
        }
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

        var plot = new Rect(50, 12, Math.Max(20, w - 62), Math.Max(20, h - 40));
        var maxSeconds = MaxDurationSeconds(primary, compare);
        var maxHz = MaxY(primary, compare);
        if (maxSeconds <= 0 || maxHz <= 0)
        {
            DrawMessage(context, LocalizationService.Instance.Get("DopplerInsights.Chart.Empty"), palette);
            return;
        }

        // Calculate viewport based on zoom and pan
        var viewportWidthSeconds = maxSeconds / _zoomLevel;
        var clampedPan = Math.Min(_panOffsetSeconds, Math.Max(0, maxSeconds - viewportWidthSeconds));
        var viewStartSeconds = clampedPan;
        var viewEndSeconds = viewStartSeconds + viewportWidthSeconds;

        DrawGrid(context, plot, palette);
        DrawAxes(context, plot, palette, viewStartSeconds, viewEndSeconds, maxHz);

        DrawSeries(context, primary, plot, viewStartSeconds, viewEndSeconds, maxHz, s => s.AbsRxDeltaHz, _renderCache.GetPen(PrimaryRxColor, 1.8));
        DrawSeries(context, primary, plot, viewStartSeconds, viewEndSeconds, maxHz, s => s.AbsTxDeltaHz, _renderCache.GetPen(PrimaryTxColor, 1.8));
        DrawSeries(context, primary, plot, viewStartSeconds, viewEndSeconds, maxHz, s => s.EffectiveThresholdHz, _renderCache.GetPen(PrimaryThresholdColor, 1.5));
        DrawWriteTicks(context, primary, plot, viewStartSeconds, viewEndSeconds, _renderCache.GetPen(PrimaryWriteColor, 1.0), 0);

        if (compare.Count > 0)
        {
            DrawSeries(context, compare, plot, viewStartSeconds, viewEndSeconds, maxHz, s => s.AbsRxDeltaHz, _renderCache.GetDashedPen(CompareRxColor, 1.3));
            DrawSeries(context, compare, plot, viewStartSeconds, viewEndSeconds, maxHz, s => s.AbsTxDeltaHz, _renderCache.GetDashedPen(CompareTxColor, 1.3));
            DrawSeries(context, compare, plot, viewStartSeconds, viewEndSeconds, maxHz, s => s.EffectiveThresholdHz, _renderCache.GetDashedPen(CompareThresholdColor, 1.2));
            DrawWriteTicks(context, compare, plot, viewStartSeconds, viewEndSeconds, _renderCache.GetPen(CompareWriteColor, 1.0), 3);
        }
    }

    private static double MaxDurationSeconds(IReadOnlyList<DopplerInsightChartSample> primary, IReadOnlyList<DopplerInsightChartSample> compare)
    {
        var p = primary.Count == 0 ? 0 : primary.Max(s => s.SecondsFromStart);
        var c = compare.Count == 0 ? 0 : compare.Max(s => s.SecondsFromStart);
        return Math.Max(1, Math.Max(p, c));
    }

    private static double MaxY(IReadOnlyList<DopplerInsightChartSample> primary, IReadOnlyList<DopplerInsightChartSample> compare)
    {
        static IEnumerable<double> Values(IReadOnlyList<DopplerInsightChartSample> samples)
        {
            foreach (var s in samples)
            {
                yield return s.AbsRxDeltaHz;
                yield return s.AbsTxDeltaHz;
                yield return s.EffectiveThresholdHz;
            }
        }

        var max = Values(primary).Concat(Values(compare)).DefaultIfEmpty(0).Max();
        return max <= 0 ? 0 : max * 1.1;
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

    private static void DrawSeries(
        DrawingContext context,
        IReadOnlyList<DopplerInsightChartSample> samples,
        Rect plot,
        double viewStartSeconds,
        double viewEndSeconds,
        double maxHz,
        Func<DopplerInsightChartSample, double> selector,
        Pen pen)
    {
        if (samples.Count < 2)
            return;

        var viewportWidth = viewEndSeconds - viewStartSeconds;
        if (viewportWidth <= 0)
            return;

        Point? previous = null;
        foreach (var sample in samples)
        {
            // Skip samples outside current viewport
            if (sample.SecondsFromStart < viewStartSeconds || sample.SecondsFromStart > viewEndSeconds)
            {
                previous = null;
                continue;
            }

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
            var y2 = plot.Bottom - 6 - yOffset;
            context.DrawLine(pen, new Point(x, y1), new Point(x, y2));
        }
    }

    private static void DrawMessage(DrawingContext context, string text, UiPalette palette)
    {
        var formatted = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold),
            12,
            new SolidColorBrush(palette.SkyPlotMessage));
        context.DrawText(formatted, new Point(14, 14));
    }

    private static void DrawAxes(DrawingContext context, Rect plot, UiPalette palette, double viewStartSeconds, double viewEndSeconds, double maxHz)
    {
        var labelBrush = new SolidColorBrush(palette.SkyPlotLabel);
        var yLabel = new FormattedText(
            $"0-{Math.Round(maxHz)} Hz",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold),
            11,
            labelBrush);
        context.DrawText(yLabel, new Point(4, plot.Y - 2));

        var left = new FormattedText(
            $"{Math.Round(viewStartSeconds)}s",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Normal),
            10,
            labelBrush);
        context.DrawText(left, new Point(plot.X, plot.Bottom + 3));

        var right = new FormattedText(
            $"{Math.Round(viewEndSeconds)}s",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Normal),
            10,
            labelBrush);
        context.DrawText(right, new Point(Math.Max(plot.X, plot.Right - right.Width), plot.Bottom + 3));
    }
}