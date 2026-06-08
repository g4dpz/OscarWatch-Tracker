using System.Collections.Immutable;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;

namespace OscarWatch.Controls;

public class WorldMapControl : ThemeAwareControl
{
    private const double WrapEdgeMarginPx = 60;
    /// <summary>Cap decoded map size — full-res Blue Marble can exceed 100 MB in memory.</summary>
    private const int MapDecodeMaxWidth = 2048;

    public static readonly StyledProperty<IReadOnlyList<SatelliteTrackState>?> TrackStatesProperty =
        AvaloniaProperty.Register<WorldMapControl, IReadOnlyList<SatelliteTrackState>?>(
            nameof(TrackStates));

    public static readonly StyledProperty<GroundStation?> GroundStationProperty =
        AvaloniaProperty.Register<WorldMapControl, GroundStation?>(nameof(GroundStation));

    public static readonly StyledProperty<string?> FocusedNoradIdProperty =
        AvaloniaProperty.Register<WorldMapControl, string?>(nameof(FocusedNoradId));

    public static readonly StyledProperty<bool> ShowFootprintMotionArrowsProperty =
        AvaloniaProperty.Register<WorldMapControl, bool>(nameof(ShowFootprintMotionArrows), true);

    public static readonly StyledProperty<GeoCoordinate?> RemoteStationProperty =
        AvaloniaProperty.Register<WorldMapControl, GeoCoordinate?>(nameof(RemoteStation));

    public static readonly StyledProperty<bool> SoloFocusedSatelliteProperty =
        AvaloniaProperty.Register<WorldMapControl, bool>(nameof(SoloFocusedSatellite));

    public static readonly StyledProperty<bool> ShowGreylineOverlayProperty =
        AvaloniaProperty.Register<WorldMapControl, bool>(nameof(ShowGreylineOverlay));

    public static readonly StyledProperty<DateTime> MapDisplayUtcProperty =
        AvaloniaProperty.Register<WorldMapControl, DateTime>(
            nameof(MapDisplayUtc),
            defaultValue: DateTime.UtcNow);

    private Bitmap? _mapBitmap;
    private INotifyCollectionChanged? _trackStatesSource;
    private Size _lastLayoutInvalidationSize;

    private Color _cachedTwilightBaseColor;
    private int _cachedTwilightBandCount;
    private ImmutableArray<SolidColorBrush> _twilightBrushes = ImmutableArray<SolidColorBrush>.Empty;

    private LabelOrderBuffer _labelOrderBuffer = new(64);
    private readonly RenderResourceCache _renderCache = new();
    private readonly FormattedTextCache _labelCache = new();

    public WorldMapControl()
    {
        ClipToBounds = true;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    static WorldMapControl()
    {
        AffectsRender<WorldMapControl>(
            TrackStatesProperty,
            GroundStationProperty,
            FocusedNoradIdProperty,
            ShowFootprintMotionArrowsProperty,
            RemoteStationProperty,
            SoloFocusedSatelliteProperty,
            ShowGreylineOverlayProperty,
            MapDisplayUtcProperty);
    }

    public bool ShowFootprintMotionArrows
    {
        get => GetValue(ShowFootprintMotionArrowsProperty);
        set => SetValue(ShowFootprintMotionArrowsProperty, value);
    }

    private const double HitRadiusPx = 16;

    public IReadOnlyList<SatelliteTrackState>? TrackStates
    {
        get => GetValue(TrackStatesProperty);
        set => SetValue(TrackStatesProperty, value);
    }

    public GroundStation? GroundStation
    {
        get => GetValue(GroundStationProperty);
        set => SetValue(GroundStationProperty, value);
    }

    public string? FocusedNoradId
    {
        get => GetValue(FocusedNoradIdProperty);
        set => SetValue(FocusedNoradIdProperty, value);
    }

    public GeoCoordinate? RemoteStation
    {
        get => GetValue(RemoteStationProperty);
        set => SetValue(RemoteStationProperty, value);
    }

    public bool SoloFocusedSatellite
    {
        get => GetValue(SoloFocusedSatelliteProperty);
        set => SetValue(SoloFocusedSatelliteProperty, value);
    }

    public bool ShowGreylineOverlay
    {
        get => GetValue(ShowGreylineOverlayProperty);
        set => SetValue(ShowGreylineOverlayProperty, value);
    }

    public DateTime MapDisplayUtc
    {
        get => GetValue(MapDisplayUtcProperty);
        set => SetValue(MapDisplayUtcProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LayoutUpdated += OnLayoutUpdatedForRender;
        if (Application.Current is not null)
            Application.Current.ActualThemeVariantChanged += OnThemeChangedClearCache;
        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Loaded);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdatedForRender;
        if (Application.Current is not null)
            Application.Current.ActualThemeVariantChanged -= OnThemeChangedClearCache;
        UnsubscribeTrackStatesSource();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TrackStatesProperty)
            BindTrackStatesSource(change.NewValue);

        if (change.Property == TrackStatesProperty || change.Property == FocusedNoradIdProperty)
            TrackingPlotAccessibility.UpdateName(this, "World map", TrackStates, FocusedNoradId);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.NewSize.Width > 0 && e.NewSize.Height > 0)
            InvalidateVisual();
    }

    private void BindTrackStatesSource(object? value)
    {
        UnsubscribeTrackStatesSource();
        _trackStatesSource = value as INotifyCollectionChanged;
        if (_trackStatesSource is not null)
            _trackStatesSource.CollectionChanged += OnTrackStatesSourceChanged;

        _renderCache.Clear();
        _labelCache.Clear();
        InvalidateVisual();
    }

    private void UnsubscribeTrackStatesSource()
    {
        if (_trackStatesSource is null)
            return;

        _trackStatesSource.CollectionChanged -= OnTrackStatesSourceChanged;
        _trackStatesSource = null;
    }

    private void OnTrackStatesSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _renderCache.Clear();
        _labelCache.Clear();
        InvalidateVisual();
    }

    private void OnThemeChangedClearCache(object? sender, EventArgs e)
    {
        _renderCache.Clear();
        _labelCache.Clear();
    }

    private void OnLayoutUpdatedForRender(object? sender, EventArgs e)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0)
            return;

        if (Math.Abs(w - _lastLayoutInvalidationSize.Width) < 0.5
            && Math.Abs(h - _lastLayoutInvalidationSize.Height) < 0.5)
            return;

        _lastLayoutInvalidationSize = new Size(w, h);
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var pos = e.GetPosition(this);
        FocusedNoradId = HitTestSatellite(pos, Bounds.Width, Bounds.Height);
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
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        using (context.PushClip(new Rect(bounds.Size)))
        {
            RenderMapContent(context, bounds.Width, bounds.Height);
        }
    }

    private void RenderMapContent(DrawingContext context, double w, double h)
    {
        var palette = UiPaletteResolver.Current;
        EnsureMapLoaded();

        if (_mapBitmap is not null)
        {
            var src = new Rect(0, 0, _mapBitmap.PixelSize.Width, _mapBitmap.PixelSize.Height);
            context.DrawImage(_mapBitmap, src, new Rect(0, 0, w, h));
        }
        else
        {
            context.FillRectangle(new SolidColorBrush(palette.MapFallbackBackground), new Rect(0, 0, w, h));
            var noMap = new FormattedText(
                "world_map.jpg not found in Assets/Maps",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter"),
                14,
                new SolidColorBrush(palette.MapLabelForeground));
            context.DrawText(noMap, new Point(12, 12));
        }

        // Layer order: base map → greyline → tracks/footprints/markers → labels (footprints must stay above greyline).
        if (ShowGreylineOverlay)
            DrawGreylineOverlay(context, MapDisplayUtc, w, h, palette);

        if (GroundStation is { } gs)
        {
            var (gx, gy) = EquirectangularProjection.GeoToPixel(gs.LatitudeDeg, gs.LongitudeDeg, w, h);
            DrawGroundStationDot(context, gx, gy, palette);
        }

        if (RemoteStation is { } remote)
        {
            var (rx, ry) = EquirectangularProjection.GeoToPixel(remote.LatitudeDeg, remote.LongitudeDeg, w, h);
            foreach (var xOffset in GetSubpointWrapOffsets(rx, w))
                PlotMarkerDrawing.DrawRemoteStationMarker(context, rx + xOffset, ry);
        }

        var states = TrackStates;
        if (states is null)
            return;

        // Pass 1: tracks, footprints, and subpoints (no labels yet).
        for (var i = 0; i < states.Count; i++)
        {
            var state = states[i];
            if (!TrackingPlotAccessibility.IsPlotSatelliteVisible(SoloFocusedSatellite, FocusedNoradId, state.NoradId))
                continue;

            var color = PlotColors.ForIndex(i);
            var isFocused = state.NoradId == FocusedNoradId;

            if (isFocused)
                DrawPolylineSegments(context, state.GroundTrack, w, h, color, 2);

            var (sx, sy) = EquirectangularProjection.GeoToPixel(
                state.Subpoint.LatitudeDeg, state.Subpoint.LongitudeDeg, w, h);

            if (state.Footprint.Count >= 3)
            {
                var fill = Color.FromArgb(72, color.R, color.G, color.B);
                var stroke = Color.FromArgb(200, color.R, color.G, color.B);
                DrawFootprint(
                    context,
                    state.Footprint,
                    state.Subpoint,
                    state.FootprintRadiusDeg,
                    w,
                    h,
                    fill,
                    stroke,
                    2);

                var heading = ShowFootprintMotionArrows
                    ? GroundTrackHeading.EstimateHeadingDeg(state.Subpoint, state.GroundTrack)
                    : null;
                if (heading is { } headingDeg)
                {
                    foreach (var xOffset in GetSubpointWrapOffsets(sx, w))
                    {
                        PlotMarkerDrawing.DrawFootprintMotionArrow(
                            context,
                            state.Subpoint,
                            headingDeg,
                            state.FootprintRadiusDeg,
                            sx + xOffset,
                            sy,
                            w,
                            h,
                            color,
                            isFocused);
                    }
                }
            }

            foreach (var xOffset in GetSubpointWrapOffsets(sx, w))
            {
                PlotMarkerDrawing.DrawSatelliteMarker(
                    context, sx + xOffset, sy, color, isFocused);
            }
        }

        // Pass 2: labels on top (non-focused first, focused last).
        // Use the same subpoint projection and map-wrap copies as the markers in pass 1.
        _labelOrderBuffer.Build(states, FocusedNoradId, SoloFocusedSatellite);

        foreach (var i in _labelOrderBuffer.Indices)
        {
            var state = states[i];
            var isFocused = string.Equals(state.NoradId, FocusedNoradId, StringComparison.Ordinal);
            var (sx, sy) = EquirectangularProjection.GeoToPixel(
                state.Subpoint.LatitudeDeg, state.Subpoint.LongitudeDeg, w, h);

            foreach (var xOffset in GetSubpointWrapOffsets(sx, w))
            {
                DrawSatelliteLabel(
                    context,
                    state.Name,
                    sx + xOffset,
                    sy,
                    palette,
                    isFocused ? 12 : 11);
            }
        }

        _labelCache.Evict(states);
    }

    private string? HitTestSatellite(Point pos, double w, double h)
    {
        var states = TrackStates;
        if (states is null)
            return null;

        string? bestId = null;
        var bestDist = double.MaxValue;

        foreach (var state in states)
        {
            if (!TrackingPlotAccessibility.IsPlotSatelliteVisible(SoloFocusedSatellite, FocusedNoradId, state.NoradId))
                continue;

            var (sx, sy) = EquirectangularProjection.GeoToPixel(
                state.Subpoint.LatitudeDeg, state.Subpoint.LongitudeDeg, w, h);

            foreach (var xOffset in GetSubpointWrapOffsets(sx, w))
            {
                var dx = sx + xOffset;
                if (dx < -WrapEdgeMarginPx || dx > w + WrapEdgeMarginPx)
                    continue;

                var dist = Math.Sqrt(Math.Pow(pos.X - dx, 2) + Math.Pow(pos.Y - sy, 2));
                if (dist <= HitRadiusPx && dist < bestDist)
                {
                    bestDist = dist;
                    bestId = state.NoradId;
                }
            }
        }

        return bestId;
    }

    private void EnsureMapLoaded()
    {
        if (_mapBitmap is not null)
            return;

        try
        {
            var uri = new Uri("avares://OscarWatch/Assets/Maps/world_map.jpg");
            using var stream = AssetLoader.Open(uri);
            _mapBitmap = Bitmap.DecodeToWidth(stream, MapDecodeMaxWidth);
        }
        catch
        {
            // map asset missing
        }
    }

    private static (double MinX, double MaxX) GetPixelXRange(
        IReadOnlyList<GeoCoordinate> points,
        double w,
        double h)
    {
        var minX = double.MaxValue;
        var maxX = double.MinValue;

        foreach (var p in points)
        {
            var (x, _) = EquirectangularProjection.GeoToPixel(p.LatitudeDeg, p.LongitudeDeg, w, h);
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
        }

        return (minX, maxX);
    }

    /// <summary>
    /// Duplicate geometry only when the path crosses the antimeridian, not when it merely spans
    /// wide longitude near a pole (which already fills the map width in pixel space).
    /// </summary>
    private static IEnumerable<double> GetHorizontalWrapOffsets(
        IReadOnlyList<GeoCoordinate> points,
        double w,
        double h)
    {
        yield return 0;

        if (!EquirectangularProjection.CrossesAntimeridian(points))
            yield break;

        var (minX, maxX) = GetPixelXRange(points, w, h);
        if (minX < WrapEdgeMarginPx)
            yield return w;
        if (maxX > w - WrapEdgeMarginPx)
            yield return -w;
    }

    /// <summary>
    /// Footprints are projected in a local plane at the subpoint; duplicate when the subpoint
    /// or projected ring reaches the map edge, but not for polar caps that already span the width.
    /// </summary>
    private static IEnumerable<double> GetFootprintWrapOffsets(
        GeoCoordinate subpoint,
        IReadOnlyList<GeoCoordinate> footprintRing,
        IReadOnlyList<(double X, double Y)> pixels,
        double footprintRadiusDeg,
        double w,
        double h)
    {
        var minX = double.MaxValue;
        var maxX = double.MinValue;
        foreach (var p in pixels)
        {
            minX = Math.Min(minX, p.X);
            maxX = Math.Max(maxX, p.X);
        }

        var span = maxX - minX;
        var isPolarCap = FootprintGeometry.ContainsNorthPole(subpoint, footprintRadiusDeg)
            || FootprintGeometry.ContainsSouthPole(subpoint, footprintRadiusDeg);

        if (isPolarCap || span > w * 0.75)
        {
            yield return 0;
            yield break;
        }

        var offsets = new HashSet<double> { 0 };

        var (sx, _) = EquirectangularProjection.GeoToPixel(
            subpoint.LatitudeDeg, subpoint.LongitudeDeg, w, h);
        foreach (var offset in GetSubpointWrapOffsets(sx, w))
            offsets.Add(offset);

        var allowPixelWrap = span < w * 0.85
            || EquirectangularProjection.CrossesAntimeridian(footprintRing);

        if (allowPixelWrap)
        {
            if (minX < WrapEdgeMarginPx)
                offsets.Add(w);
            if (maxX > w - WrapEdgeMarginPx)
                offsets.Add(-w);
        }

        foreach (var offset in offsets)
            yield return offset;
    }

    /// <summary>
    /// Subpoints and labels use a single lon→x projection; near the map edges duplicate them
    /// with the same ±width offsets as footprints so they stay aligned with the visible wrap.
    /// </summary>
    private static IEnumerable<double> GetSubpointWrapOffsets(double sx, double w)
    {
        yield return 0;

        if (sx < WrapEdgeMarginPx)
            yield return w;

        if (sx > w - WrapEdgeMarginPx)
            yield return -w;
    }

    /// <summary>
    /// Draw the visibility footprint as a geographic ring (equirectangular), with a dedicated
    /// polar-cap shape when the footprint includes a pole.
    /// </summary>
    private void DrawFootprint(
        DrawingContext context,
        IReadOnlyList<GeoCoordinate> footprint,
        GeoCoordinate subpoint,
        double footprintRadiusDeg,
        double w,
        double h,
        Color fillColor,
        Color strokeColor,
        double strokeWidth)
    {
        if (footprint.Count < 3)
            return;

        if (w <= 0 || h <= 0)
            return;

        var radiusDeg = footprintRadiusDeg > 0
            ? footprintRadiusDeg
            : FootprintGeometry.EstimateRingRadiusDeg(subpoint, footprint);

        var fillBrush = _renderCache.GetBrush(fillColor);
        var pen = _renderCache.GetPen(strokeColor, strokeWidth);
        var pixels = FootprintGeometry.ProjectRingToMap(
            subpoint, footprint, radiusDeg, w, h);
        if (pixels.Count < 3)
            return;

        foreach (var xOffset in GetFootprintWrapOffsets(subpoint, footprint, pixels, radiusDeg, w, h))
        {
            var geometry = BuildFootprintGeometry(pixels, xOffset);
            context.DrawGeometry(fillBrush, pen, geometry);
        }
    }

    private static StreamGeometry BuildFootprintGeometry(
        IReadOnlyList<(double X, double Y)> pixels,
        double xOffset)
    {
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.SetFillRule(FillRule.NonZero);
            var first = pixels[0];
            g.BeginFigure(new Point(first.X + xOffset, first.Y), true);
            for (var i = 1; i < pixels.Count; i++)
            {
                var p = pixels[i];
                g.LineTo(new Point(p.X + xOffset, p.Y));
            }

            g.EndFigure(true);
        }

        return geometry;
    }

    private void DrawGreylineOverlay(
        DrawingContext context,
        DateTime mapUtc,
        double w,
        double h,
        UiPalette palette)
    {
        if (w <= 0 || h <= 0)
            return;

        var geometry = DayNightTerminator.GetGeometry(
            DateTime.SpecifyKind(mapUtc, DateTimeKind.Utc));

        var fillBrush = _renderCache.GetBrush(palette.GreylineNightFill);
        DrawNightFillScanlines(context, geometry, w, h, fillBrush);

        if (geometry.DrawTerminatorLine && geometry.Terminator.Count >= 2)
        {
            DrawPolylineSegments(
                context,
                geometry.Terminator,
                w,
                h,
                palette.GreylineTerminatorStroke,
                1.0);
        }
    }

    /// <summary>
    /// Shade the night hemisphere column-by-column. Avoids polygon fill on the full-world
    /// equirectangular map, which breaks apart under antimeridian splitting.
    /// </summary>
    private void DrawNightFillScanlines(
        DrawingContext context,
        DayNightGeometry geometry,
        double w,
        double h,
        IBrush fillBrush)
    {
        if (geometry.FullNightHalf)
        {
            var y0 = geometry.NightTowardSouth ? h * 0.5 : 0;
            var y1 = geometry.NightTowardSouth ? h : h * 0.5;
            context.FillRectangle(fillBrush, new Rect(0, y0, w, y1 - y0));
            return;
        }

        if (geometry.Terminator.Count < 2)
            return;

        var lonStep = DayNightTerminator.LongitudeStepDeg;
        var columnCount = (int)Math.Ceiling(360.0 / lonStep);
        var stripWidth = w / Math.Max(1, columnCount - 1) + 1.5;
        const double twilightFadePx = 28;
        const int twilightBands = 5;

        var twilightBrushes = fillBrush is SolidColorBrush solid
            ? GetTwilightBrushes(solid, twilightBands)
            : ImmutableArray<SolidColorBrush>.Empty;

        for (var i = 0; i < columnCount; i++)
        {
            var lon = -180.0 + i * (360.0 / (columnCount - 1));
            var termLat = InterpolateTerminatorLatitude(geometry.Terminator, lon);
            var (x, yTerm) = EquirectangularProjection.GeoToPixel(termLat, lon, w, h);

            if (geometry.NightTowardSouth)
                DrawNightColumnSouth(context, fillBrush, x, yTerm, h, w, stripWidth, twilightFadePx, twilightBrushes);
            else
                DrawNightColumnNorth(context, fillBrush, x, yTerm, h, w, stripWidth, twilightFadePx, twilightBrushes);
        }
    }

    private ImmutableArray<SolidColorBrush> GetTwilightBrushes(SolidColorBrush baseBrush, int twilightBands)
    {
        if (_twilightBrushes.Length == twilightBands
            && _cachedTwilightBaseColor == baseBrush.Color
            && _cachedTwilightBandCount == twilightBands)
            return _twilightBrushes;

        var brushes = new SolidColorBrush[twilightBands];
        for (var band = 0; band < twilightBands; band++)
        {
            var t = (band + 0.5) / twilightBands;
            var alpha = (byte)(baseBrush.Color.A * (0.15 + 0.35 * t));
            brushes[band] = new SolidColorBrush(
                Color.FromArgb(alpha,
                    baseBrush.Color.R,
                    baseBrush.Color.G,
                    baseBrush.Color.B));
        }

        _cachedTwilightBaseColor = baseBrush.Color;
        _cachedTwilightBandCount = twilightBands;
        _twilightBrushes = ImmutableArray.Create(brushes);
        return _twilightBrushes;
    }

    private static void DrawNightColumnSouth(
        DrawingContext context,
        IBrush baseBrush,
        double x,
        double yTerm,
        double h,
        double w,
        double stripWidth,
        double twilightFadePx,
        ImmutableArray<SolidColorBrush> twilightBrushes)
    {
        var twilightBands = twilightBrushes.Length;
        var bodyStart = Math.Min(h, yTerm + twilightFadePx);
        if (h - bodyStart >= 0.5)
            DrawNightStrip(context, baseBrush, x, bodyStart, h, w, stripWidth);

        if (twilightBands == 0)
            return;

        var bandHeight = twilightFadePx / twilightBands;
        for (var band = 0; band < twilightBands; band++)
        {
            var y0 = yTerm + band * bandHeight;
            var y1 = yTerm + (band + 1) * bandHeight;
            DrawNightStrip(context, twilightBrushes[band], x, y0, y1, w, stripWidth);
        }
    }

    private static void DrawNightColumnNorth(
        DrawingContext context,
        IBrush baseBrush,
        double x,
        double yTerm,
        double h,
        double w,
        double stripWidth,
        double twilightFadePx,
        ImmutableArray<SolidColorBrush> twilightBrushes)
    {
        var twilightBands = twilightBrushes.Length;
        var bodyEnd = Math.Max(0, yTerm - twilightFadePx);
        if (bodyEnd >= 0.5)
            DrawNightStrip(context, baseBrush, x, 0, bodyEnd, w, stripWidth);

        if (twilightBands == 0)
            return;

        var bandHeight = twilightFadePx / twilightBands;
        for (var band = 0; band < twilightBands; band++)
        {
            var y1 = yTerm - band * bandHeight;
            var y0 = yTerm - (band + 1) * bandHeight;
            DrawNightStrip(context, twilightBrushes[band], x, y0, y1, w, stripWidth);
        }
    }

    private static void DrawNightStrip(
        DrawingContext context,
        IBrush brush,
        double x,
        double yStart,
        double yEnd,
        double w,
        double stripWidth)
    {
        if (yEnd - yStart < 0.5)
            return;

        var rect = new Rect(x - stripWidth * 0.5, yStart, stripWidth, yEnd - yStart);
        context.FillRectangle(brush, rect);

        if (x < WrapEdgeMarginPx)
            context.FillRectangle(brush, new Rect(rect.X + w, rect.Y, rect.Width, rect.Height));
        if (x > w - WrapEdgeMarginPx)
            context.FillRectangle(brush, new Rect(rect.X - w, rect.Y, rect.Width, rect.Height));
    }

    private static double InterpolateTerminatorLatitude(
        IReadOnlyList<GeoCoordinate> terminator,
        double longitudeDeg)
    {
        if (terminator.Count == 0)
            return 0;

        var lo = 0;
        var hi = terminator.Count - 1;

        // Early-out: outside the stored longitude range.
        if (longitudeDeg <= terminator[lo].LongitudeDeg)
            return terminator[lo].LatitudeDeg;
        if (longitudeDeg >= terminator[hi].LongitudeDeg)
            return terminator[hi].LatitudeDeg;

        // Binary search for bracketing pair.
        while (hi - lo > 1)
        {
            var mid = (lo + hi) >> 1;
            if (terminator[mid].LongitudeDeg <= longitudeDeg)
                lo = mid;
            else
                hi = mid;
        }

        var before = terminator[lo];
        var after = terminator[hi];
        if (Math.Abs(after.LongitudeDeg - before.LongitudeDeg) < 0.01)
            return before.LatitudeDeg;

        var t = (longitudeDeg - before.LongitudeDeg)
              / (after.LongitudeDeg - before.LongitudeDeg);
        return before.LatitudeDeg + t * (after.LatitudeDeg - before.LatitudeDeg);
    }

    private void DrawPolylineSegments(
        DrawingContext context,
        IReadOnlyList<GeoCoordinate> points,
        double w,
        double h,
        Color color,
        double thickness,
        bool close = false)
    {
        if (points.Count < 2)
            return;

        var pen = _renderCache.GetPen(color, thickness);

        foreach (var xOffset in GetHorizontalWrapOffsets(points, w, h))
            DrawPolylineOffset(context, points, w, h, xOffset, pen, close);
    }

    private static void DrawPolylineOffset(
        DrawingContext context,
        IReadOnlyList<GeoCoordinate> points,
        double w,
        double h,
        double xOffset,
        Pen pen,
        bool close)
    {
        var maxDx = w / 2.0;
        var maxDy = h / 3.0;

        foreach (var chain in EquirectangularProjection.SplitForMapDraw(points, w, h))
        {
            if (chain.Count < 2)
                continue;

            for (var i = 0; i < chain.Count - 1; i++)
            {
                var p0 = chain[i];
                var p1 = chain[i + 1];
                context.DrawLine(
                    pen,
                    new Point(p0.X + xOffset, p0.Y),
                    new Point(p1.X + xOffset, p1.Y));
            }

            if (close && chain.Count >= 2)
            {
                var first = chain[0];
                var last = chain[^1];
                if (Math.Abs(first.X - last.X) <= maxDx && Math.Abs(first.Y - last.Y) <= maxDy)
                {
                    context.DrawLine(
                        pen,
                        new Point(last.X + xOffset, last.Y),
                        new Point(first.X + xOffset, first.Y));
                }
            }
        }
    }

    private static void DrawGroundStationDot(DrawingContext context, double x, double y, UiPalette palette)
    {
        PlotMarkerDrawing.DrawGroundStationMarker(context, x, y, palette);
    }

    private void DrawSatelliteLabel(
        DrawingContext context,
        string name,
        double x,
        double y,
        UiPalette palette,
        double fontSize = 12)
    {
        var text = _labelCache.Get(name, fontSize, palette);

        var tx = x - text.Width / 2;
        var ty = y - text.Height - 8;
        var bg = new Rect(tx - 4, ty - 2, text.Width + 8, text.Height + 4);
        context.FillRectangle(_labelCache.GetBackgroundBrush(palette), bg);
        context.DrawText(text, new Point(tx, ty));
    }
}
