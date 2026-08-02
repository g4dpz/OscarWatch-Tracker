using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using OscarWatch.Core.Display;
using OscarWatch.Core.Models;

namespace OscarWatch.Controls;

/// <summary>Click/drag polar editor for a station horizon mask.</summary>
public sealed class HorizonMaskPolarEditControl : ThemeAwareControl
{
    private const double LabelMarginPx = 14;
    private const double HitRadiusPx = 12;
    private const double HandleRadiusPx = 5;

    private readonly RenderResourceCache _renderCache = new();
    private int _dragIndex = -1;
    private ObservableCollection<HorizonMaskPoint>? _pointsSource;

    public static readonly StyledProperty<ObservableCollection<HorizonMaskPoint>?> PointsProperty =
        AvaloniaProperty.Register<HorizonMaskPolarEditControl, ObservableCollection<HorizonMaskPoint>?>(nameof(Points));

    static HorizonMaskPolarEditControl()
    {
        AffectsRender<HorizonMaskPolarEditControl>(PointsProperty);
    }

    public HorizonMaskPolarEditControl()
    {
        ClipToBounds = true;
        MinHeight = 220;
        Focusable = true;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += (_, _) => _dragIndex = -1;
        KeyDown += OnKeyDown;
    }

    public ObservableCollection<HorizonMaskPoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != PointsProperty)
            return;

        if (_pointsSource is not null)
        {
            _pointsSource.CollectionChanged -= OnPointsCollectionChanged;
            foreach (var p in _pointsSource)
                p.PropertyChanged -= OnPointPropertyChanged;
        }

        _pointsSource = Points;
        if (_pointsSource is not null)
        {
            _pointsSource.CollectionChanged += OnPointsCollectionChanged;
            foreach (var p in _pointsSource)
                p.PropertyChanged += OnPointPropertyChanged;
        }
        InvalidateVisual();
    }

    private void OnPointPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        InvalidateVisual();

    private void OnPointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (HorizonMaskPoint p in e.OldItems)
                p.PropertyChanged -= OnPointPropertyChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (HorizonMaskPoint p in e.NewItems)
                p.PropertyChanged += OnPointPropertyChanged;
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0)
            return;

        var palette = UiPaletteResolver.Current;
        var (cx, cy, plotRadius) = GetPlotGeometry(w, h);
        context.FillRectangle(_renderCache.GetBrush(palette.SkyPlotBackground), new Rect(0, 0, w, h));

        var disk = new EllipseGeometry(new Rect(cx - plotRadius, cy - plotRadius, plotRadius * 2, plotRadius * 2));
        context.DrawGeometry(
            _renderCache.GetBrush(palette.SkyPlotBackground),
            _renderCache.GetPen(palette.SkyPlotBorder, 1.5),
            disk);

        DrawRing(context, cx, cy, plotRadius, 30, palette.SkyPlotRing30);
        DrawRing(context, cx, cy, plotRadius, 60, palette.SkyPlotRing60);

        var points = Points;
        if (points is { Count: > 0 })
        {
            var mask = new HorizonMask { Points = points.ToList() };
            HorizonMaskPlotDrawing.DrawObstruction(context, cx, cy, plotRadius, mask, _renderCache);

            foreach (var p in points)
            {
                if (!HorizonMaskEditMath.TryAzElToPoint(cx, cy, plotRadius, p.AzimuthDeg, p.ElevationDeg, out var pt))
                    continue;
                var rect = new Rect(pt.X - HandleRadiusPx, pt.Y - HandleRadiusPx, HandleRadiusPx * 2, HandleRadiusPx * 2);
                context.DrawEllipse(
                    _renderCache.GetBrush(Color.Parse("#4DA3FF")),
                    _renderCache.GetPen(Colors.White, 1.5),
                    rect);
            }
        }

        DrawCardinals(context, cx, cy, plotRadius, palette);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var points = Points;
        if (points is null)
            return;

        var pos = e.GetPosition(this);
        var (cx, cy, plotRadius) = GetPlotGeometry(Bounds.Width, Bounds.Height);
        var list = points.Select(p => (p.AzimuthDeg, p.ElevationDeg)).ToList();
        var hit = HorizonMaskEditMath.FindNearestPointIndex(list, cx, cy, plotRadius, pos.X, pos.Y, HitRadiusPx);

        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            if (hit >= 0 && hit < points.Count)
            {
                points.RemoveAt(hit);
                InvalidateVisual();
            }

            e.Handled = true;
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (hit >= 0)
        {
            _dragIndex = hit;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (!HorizonMaskEditMath.TryPointToAzEl(cx, cy, plotRadius, pos.X, pos.Y, out var az, out var el))
            return;

        var insertAt = 0;
        while (insertAt < points.Count && points[insertAt].AzimuthDeg < az - 1e-9)
            insertAt++;

        // Replace near-duplicate azimuth instead of stacking.
        if (insertAt < points.Count && Math.Abs(points[insertAt].AzimuthDeg - az) < 0.6)
        {
            points[insertAt].ElevationDeg = el;
            _dragIndex = insertAt;
        }
        else if (insertAt > 0 && Math.Abs(points[insertAt - 1].AzimuthDeg - az) < 0.6)
        {
            points[insertAt - 1].ElevationDeg = el;
            _dragIndex = insertAt - 1;
        }
        else
        {
            points.Insert(insertAt, new HorizonMaskPoint(az, el));
            _dragIndex = insertAt;
        }

        e.Pointer.Capture(this);
        InvalidateVisual();
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragIndex < 0 || Points is null || _dragIndex >= Points.Count)
            return;

        var pos = e.GetPosition(this);
        var (cx, cy, plotRadius) = GetPlotGeometry(Bounds.Width, Bounds.Height);
        if (!HorizonMaskEditMath.TryPointToAzEl(cx, cy, plotRadius, pos.X, pos.Y, out var az, out var el))
            return;

        Points[_dragIndex].AzimuthDeg = az;
        Points[_dragIndex].ElevationDeg = el;
        InvalidateVisual();
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragIndex < 0 || Points is null)
            return;

        // Re-sort after drag so azimuth order stays consistent for the table.
        var ordered = Points.OrderBy(p => p.AzimuthDeg).ToList();
        Points.Clear();
        foreach (var p in ordered)
            Points.Add(p);

        _dragIndex = -1;
        e.Pointer.Capture(null);
        InvalidateVisual();
        e.Handled = true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete || Points is null || Points.Count == 0)
            return;
        Points.RemoveAt(Points.Count - 1);
        InvalidateVisual();
        e.Handled = true;
    }

    private static (double Cx, double Cy, double PlotRadius) GetPlotGeometry(double w, double h)
    {
        var size = Math.Min(w, h);
        var plotRadius = Math.Max(10, (size / 2) - LabelMarginPx);
        return (w / 2, h / 2, plotRadius);
    }

    private void DrawRing(DrawingContext context, double cx, double cy, double plotRadius, double el, Color color)
    {
        if (!HorizonMaskEditMath.TryAzElToPoint(cx, cy, plotRadius, 0, el, out var pt))
            return;
        var r = Math.Abs(pt.Y - cy);
        context.DrawEllipse(null, _renderCache.GetPen(color, 1), new Rect(cx - r, cy - r, r * 2, r * 2));
    }

    private void DrawCardinals(DrawingContext context, double cx, double cy, double plotRadius, UiPalette palette)
    {
        foreach (var (az, label) in new[] { (0.0, "N"), (90.0, "E"), (180.0, "S"), (270.0, "W") })
        {
            if (!HorizonMaskEditMath.TryAzElToPoint(cx, cy, plotRadius, az, 0, out var edge))
                continue;
            var dx = edge.X - cx;
            var dy = edge.Y - cy;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1)
                continue;
            var lx = cx + dx / len * (plotRadius + 10);
            var ly = cy + dy / len * (plotRadius + 10);
            var text = new FormattedText(
                label,
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                11,
                _renderCache.GetBrush(palette.SkyPlotBorder));
            context.DrawText(text, new Point(lx - text.Width / 2, ly - text.Height / 2));
        }
    }
}
