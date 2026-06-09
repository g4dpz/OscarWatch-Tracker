using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using OscarWatch.Core.Models;
using OscarWatch.Localization;

namespace OscarWatch.Controls;

/// <summary>
/// Polar azimuth/elevation sky plot (north up). Center = zenith, outer ring = horizon.
/// </summary>
public class SkyPlotControl : ThemeAwareControl
{
    private const double HitRadiusPx = 14;
    /// <summary>Space outside the horizon circle for N/E/S/W labels.</summary>
    private const double LabelMarginPx = 16;

    private INotifyCollectionChanged? _trackStatesSource;

    public static readonly StyledProperty<IReadOnlyList<SatelliteTrackState>?> TrackStatesProperty =
        AvaloniaProperty.Register<SkyPlotControl, IReadOnlyList<SatelliteTrackState>?>(nameof(TrackStates));

    public static readonly StyledProperty<string?> FocusedNoradIdProperty =
        AvaloniaProperty.Register<SkyPlotControl, string?>(nameof(FocusedNoradId));

    public static readonly StyledProperty<double> MinimumElevationDegProperty =
        AvaloniaProperty.Register<SkyPlotControl, double>(nameof(MinimumElevationDeg), 5.0);

    public static readonly StyledProperty<bool> SoloFocusedSatelliteProperty =
        AvaloniaProperty.Register<SkyPlotControl, bool>(nameof(SoloFocusedSatellite));

    static SkyPlotControl()
    {
        AffectsRender<SkyPlotControl>(
            TrackStatesProperty,
            FocusedNoradIdProperty,
            MinimumElevationDegProperty,
            SoloFocusedSatelliteProperty);
    }

    public SkyPlotControl()
    {
        ClipToBounds = true;
        Focusable = true;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        MinWidth = 0;
        Cursor = new Cursor(StandardCursorType.Hand);
        ToolTip.SetTip(this, LocalizationService.Instance.Get("A11y.SkyPlot.TabHint"));
    }

    public IReadOnlyList<SatelliteTrackState>? TrackStates
    {
        get => GetValue(TrackStatesProperty);
        set => SetValue(TrackStatesProperty, value);
    }

    public string? FocusedNoradId
    {
        get => GetValue(FocusedNoradIdProperty);
        set => SetValue(FocusedNoradIdProperty, value);
    }

    public double MinimumElevationDeg
    {
        get => GetValue(MinimumElevationDegProperty);
        set => SetValue(MinimumElevationDegProperty, value);
    }

    public bool SoloFocusedSatellite
    {
        get => GetValue(SoloFocusedSatelliteProperty);
        set => SetValue(SoloFocusedSatelliteProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        BindTrackStatesSource(TrackStates);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        UnsubscribeTrackStatesSource();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TrackStatesProperty)
            BindTrackStatesSource(change.NewValue);

        if (change.Property == TrackStatesProperty || change.Property == FocusedNoradIdProperty)
            TrackingPlotAccessibility.UpdateName(
                this,
                LocalizationService.Instance.Get("Main.SkyPlot"),
                TrackStates,
                FocusedNoradId);
    }

    private void BindTrackStatesSource(object? value)
    {
        UnsubscribeTrackStatesSource();
        _trackStatesSource = value as INotifyCollectionChanged;
        if (_trackStatesSource is not null)
            _trackStatesSource.CollectionChanged += OnTrackStatesSourceChanged;

        InvalidateVisual();
    }

    private void UnsubscribeTrackStatesSource()
    {
        if (_trackStatesSource is null)
            return;

        _trackStatesSource.CollectionChanged -= OnTrackStatesSourceChanged;
        _trackStatesSource = null;
    }

    private void OnTrackStatesSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        InvalidateVisual();

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var pos = e.GetPosition(this);
        FocusedNoradId = HitTestSatellite(pos);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (TrackStates is not { Count: > 0 })
            return;

        if (e.Key is Key.Enter or Key.Space)
        {
            FocusedNoradId ??= TrackStates[0].NoradId;
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Left or Key.Up)
        {
            FocusedNoradId = TrackingPlotAccessibility.CycleFocusedNoradId(
                TrackStates, FocusedNoradId, -1);
            e.Handled = true;
        }
        else if (e.Key is Key.Right or Key.Down)
        {
            FocusedNoradId = TrackingPlotAccessibility.CycleFocusedNoradId(
                TrackStates, FocusedNoradId, 1);
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
        var local = new Rect(0, 0, w, h);
        var (cx, cy, plotRadius) = GetPlotGeometry(w, h);

        context.FillRectangle(new SolidColorBrush(palette.SkyPlotBackground), local);

        DrawHorizonDisk(context, cx, cy, plotRadius, palette);
        DrawElevationRing(context, cx, cy, plotRadius, 30, palette.SkyPlotRing30, 1);
        DrawElevationRing(context, cx, cy, plotRadius, 60, palette.SkyPlotRing60, 1);

        if (MinimumElevationDeg > 0 && MinimumElevationDeg < 90)
        {
            DrawElevationRing(context, cx, cy, plotRadius, MinimumElevationDeg, palette.SkyPlotMinElRing, 1, dashed: true);
            DrawMinElevationLabel(context, cx, cy, plotRadius, MinimumElevationDeg, palette);
        }

        DrawAzimuthSpokes(context, cx, cy, plotRadius, palette);
        DrawCardinalLabels(context, cx, cy, plotRadius, palette);

        var states = TrackStates;
        if (states is null)
            return;

        var visibleCount = 0;
        for (var i = 0; i < states.Count; i++)
        {
            if (!TrackingPlotAccessibility.IsPlotSatelliteVisible(SoloFocusedSatellite, FocusedNoradId, states[i].NoradId))
                continue;

            if (states[i].LookAngles is not { } la)
                continue;

            if (!TryAzElToPoint(cx, cy, plotRadius, la.AzimuthDeg, la.ElevationDeg, out var point))
                continue;

            visibleCount++;
            var color = PlotColors.ForIndex(i);
            var isFocused = states[i].NoradId == FocusedNoradId;
            PlotMarkerDrawing.DrawSatelliteMarker(
                context,
                point.X,
                point.Y,
                color,
                isFocused,
                la.ElevationDeg < MinimumElevationDeg);
        }

        if (visibleCount == 0)
            DrawCenterMessage(
                context,
                cx,
                cy,
                LocalizationService.Instance.Get("A11y.SkyPlot.AllBelowHorizon"),
                palette);
    }

    private static (double Cx, double Cy, double PlotRadius) GetPlotGeometry(double width, double height)
    {
        var side = Math.Min(width, height);
        var cx = width / 2;
        var cy = height / 2;
        var plotRadius = Math.Max(0, side / 2 - LabelMarginPx);
        return (cx, cy, plotRadius);
    }

    private string? HitTestSatellite(Point pos)
    {
        var (cx, cy, plotRadius) = GetPlotGeometry(Bounds.Width, Bounds.Height);

        var states = TrackStates;
        if (states is null)
            return null;

        string? bestId = null;
        var bestDist = double.MaxValue;

        for (var i = 0; i < states.Count; i++)
        {
            if (!TrackingPlotAccessibility.IsPlotSatelliteVisible(SoloFocusedSatellite, FocusedNoradId, states[i].NoradId))
                continue;

            if (states[i].LookAngles is not { } la)
                continue;

            if (!TryAzElToPoint(cx, cy, plotRadius, la.AzimuthDeg, la.ElevationDeg, out var point))
                continue;

            var dist = Math.Sqrt(Math.Pow(pos.X - point.X, 2) + Math.Pow(pos.Y - point.Y, 2));
            if (dist <= HitRadiusPx && dist < bestDist)
            {
                bestDist = dist;
                bestId = states[i].NoradId;
            }
        }

        return bestId;
    }

    /// <summary>Maps az/el to plot pixels. Returns false when the satellite is below the horizon.</summary>
    internal static bool TryAzElToPoint(
        double cx,
        double cy,
        double plotRadius,
        double azimuthDeg,
        double elevationDeg,
        out (double X, double Y) point)
    {
        if (elevationDeg < 0)
        {
            point = default;
            return false;
        }

        var el = Math.Clamp(elevationDeg, 0, 90);
        var r = (90.0 - el) / 90.0 * plotRadius;
        var azRad = azimuthDeg * Math.PI / 180.0;
        point = (cx + r * Math.Sin(azRad), cy - r * Math.Cos(azRad));
        return true;
    }

    private static void DrawHorizonDisk(DrawingContext context, double cx, double cy, double plotRadius, UiPalette palette)
    {
        var disk = new Rect(cx - plotRadius, cy - plotRadius, plotRadius * 2, plotRadius * 2);
        context.DrawEllipse(
            new SolidColorBrush(palette.SkyPlotBackground),
            new Pen(new SolidColorBrush(palette.SkyPlotBorder), 1.5),
            disk);
    }

    private static void DrawElevationRing(
        DrawingContext context,
        double cx,
        double cy,
        double plotRadius,
        double elevationDeg,
        Color color,
        double thickness,
        bool dashed = false)
    {
        var r = (90.0 - Math.Clamp(elevationDeg, 0, 90)) / 90.0 * plotRadius;
        var pen = new Pen(new SolidColorBrush(color), thickness);
        if (dashed)
            pen.DashStyle = DashStyle.Dash;

        context.DrawEllipse(null, pen, new Rect(cx - r, cy - r, r * 2, r * 2));
    }

    private static void DrawAzimuthSpokes(DrawingContext context, double cx, double cy, double plotRadius, UiPalette palette)
    {
        var pen = new Pen(new SolidColorBrush(palette.SkyPlotSpoke), 1);
        for (var az = 0; az < 360; az += 45)
        {
            if (!TryAzElToPoint(cx, cy, plotRadius, az, 0, out var spokeEnd))
                continue;

            context.DrawLine(pen, new Point(cx, cy), new Point(spokeEnd.X, spokeEnd.Y));
        }
    }

    private static void DrawCardinalLabels(DrawingContext context, double cx, double cy, double plotRadius, UiPalette palette)
    {
        DrawLabel(context, "N", cx, cy - plotRadius - 14, palette);
        DrawLabel(context, "S", cx, cy + plotRadius + 4, palette);
        DrawLabel(context, "E", cx + plotRadius + 6, cy - 5, palette);
        DrawLabel(context, "W", cx - plotRadius - 18, cy - 5, palette);
    }

    private static void DrawLabel(DrawingContext context, string text, double x, double y, UiPalette palette)
    {
        var ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold),
            12,
            new SolidColorBrush(palette.SkyPlotLabel));
        context.DrawText(ft, new Point(x, y));
    }

    private static void DrawMinElevationLabel(
        DrawingContext context,
        double cx,
        double cy,
        double plotRadius,
        double minimumElevationDeg,
        UiPalette palette)
    {
        if (!TryAzElToPoint(cx, cy, plotRadius, 135, minimumElevationDeg, out var anchor))
            return;

        DrawLabel(context, $"{minimumElevationDeg:F0}° min", anchor.X + 6, anchor.Y - 6, palette);
    }

    private static void DrawCenterMessage(DrawingContext context, double cx, double cy, string text, UiPalette palette)
    {
        var ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Normal),
            12,
            new SolidColorBrush(palette.SkyPlotMessage))
        {
            MaxTextWidth = 120,
            TextAlignment = TextAlignment.Center
        };
        context.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));
    }
}
