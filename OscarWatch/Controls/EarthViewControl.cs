using System.Buffers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using OscarWatch.Core.Geo;
using Serilog;

namespace OscarWatch.Controls;

/// <summary>
/// The Earth as seen from a satellite. The globe is a raster made from the world map by
/// <see cref="GlobeRasteriser"/> on a background task, and the limb and other overlays are drawn on top as
/// vectors so lines and text stay sharp at any window size.
/// </summary>
/// <remarks>
/// The world map pixels are held through a <see cref="BasemapPixelCache"/> lease that is taken when the
/// control joins the visual tree and released when it leaves, so the memory is used only while the Earth
/// View window is open.
/// </remarks>
public sealed class EarthViewControl : ThemeAwareControl
{
    private static readonly ILogger Log = Serilog.Log.ForContext<EarthViewControl>();

    /// <summary>Limb outline: a dark outline under a bright core, as the ground track on the world map.</summary>
    private const double LimbOutlineThickness = 5.0;

    private const double LimbCoreThickness = 2.5;

    public static readonly StyledProperty<EarthViewFrame?> FrameProperty =
        AvaloniaProperty.Register<EarthViewControl, EarthViewFrame?>(nameof(Frame));

    public static readonly StyledProperty<double> ZoomFactorProperty =
        AvaloniaProperty.Register<EarthViewControl, double>(nameof(ZoomFactor), 1.0);

    private readonly RenderResourceCache _renderCache = new();
    private readonly GlobeRasteriser _rasteriser = new();

    private BasemapPixelCache.Lease? _lease;
    private bool _attached;
    private int _leaseRequest;

    private WriteableBitmap? _bitmap;
    private RasterKey? _requestedKey;
    private CancellationTokenSource? _renderCts;
    private int _generation;
    private bool _loggedRenderFailure;

    static EarthViewControl()
    {
        AffectsRender<EarthViewControl>(FrameProperty, ZoomFactorProperty);
    }

    public EarthViewControl()
    {
        ClipToBounds = true;
        MinWidth = 160;
        MinHeight = 160;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.HighQuality);
    }

    /// <summary>What to draw. Null draws only the background.</summary>
    public EarthViewFrame? Frame
    {
        get => GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    /// <summary>1 fits the limb to the control. Larger values zoom in.</summary>
    public double ZoomFactor
    {
        get => GetValue(ZoomFactorProperty);
        set => SetValue(ZoomFactorProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        BeginAcquireLease();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        _leaseRequest++; // an acquisition still in flight must dispose its lease instead of using it
        _generation++;
        _renderCts?.Cancel();
        _renderCts = null;
        _requestedKey = null;

        _lease?.Dispose();
        _lease = null;
        _bitmap?.Dispose();
        _bitmap = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnThemeChanged()
    {
        _renderCache.Clear();
        _requestedKey = null; // the fallback sea colour and shading follow the theme
        RequestRaster();
        base.OnThemeChanged();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FrameProperty || change.Property == ZoomFactorProperty)
            RequestRaster();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        RequestRaster();
    }

    /// <summary>
    /// Takes the world map lease on a background thread, because the first lease decodes a large JPEG and
    /// that must not freeze the window as it opens.
    /// </summary>
    private void BeginAcquireLease()
    {
        var request = ++_leaseRequest;
        Task.Run(() => BasemapPixelCache.Default.Acquire())
            .ContinueWith(
                task => Dispatcher.UIThread.Post(() => OnLeaseAcquired(request, task)),
                TaskScheduler.Default);
    }

    private void OnLeaseAcquired(int request, Task<BasemapPixelCache.Lease> task)
    {
        if (task.IsFaulted || task.IsCanceled)
        {
            Log.Warning(task.Exception, "Earth View could not take the world map lease");
            return;
        }

        var lease = task.Result;
        if (!_attached || request != _leaseRequest)
        {
            lease.Dispose(); // the window closed while the map was loading
            return;
        }

        _lease = lease;
        _requestedKey = null;
        RequestRaster();
    }

    private RasterKey? DesiredKey()
    {
        var frame = Frame;
        var zoom = ZoomFactor;
        var side = Math.Min(Bounds.Width, Bounds.Height);
        if (frame is null || _lease is null || side < 16 || !double.IsFinite(zoom) || zoom <= 0)
            return null;

        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        return new RasterKey(
            frame.Camera,
            GlobeRasterPolicy.RasterSize(side, scaling),
            zoom,
            GlobeRasterPolicy.UtcMinute(frame.DisplayUtc),
            Application.Current?.ActualThemeVariant == ThemeVariant.Dark,
            _lease.Pixels.IsAvailable);
    }

    /// <summary>Starts a background render if the picture on screen is no longer good enough. UI thread only.</summary>
    private void RequestRaster()
    {
        if (!_attached || _lease is null || Frame is not { } frame)
            return;

        if (DesiredKey() is not { } desired || !GlobeRasterPolicy.NeedsRender(_requestedKey, desired))
            return;

        _requestedKey = desired;
        _renderCts?.Cancel();
        var cts = new CancellationTokenSource();
        _renderCts = cts;
        var generation = ++_generation;

        var camera = desired.Camera;
        var size = desired.SizePx;
        var zoom = desired.Zoom;
        var subsolar = frame.Subsolar;
        var pixels = _lease.Pixels;
        var palette = GlobePalette.From(UiPaletteResolver.Current);
        var buffer = ArrayPool<uint>.Shared.Rent(size * size);

        Task.Run(() =>
        {
            try
            {
                _rasteriser.Render(camera, size, zoom, subsolar, shadeNight: true, palette, pixels, buffer, cts.Token);
            }
            catch (OperationCanceledException)
            {
                ArrayPool<uint>.Shared.Return(buffer);
                return;
            }
            catch (Exception ex)
            {
                ArrayPool<uint>.Shared.Return(buffer);
                Dispatcher.UIThread.Post(() => OnRenderFailed(generation, ex));
                return;
            }

            Dispatcher.UIThread.Post(() => ApplyRaster(generation, desired, buffer));
        });
    }

    private void OnRenderFailed(int generation, Exception ex)
    {
        if (generation != _generation)
            return;

        _requestedKey = null; // allow a retry on the next change
        if (_loggedRenderFailure)
            return;

        _loggedRenderFailure = true;
        Log.Warning(ex, "Earth View could not draw the globe");
    }

    private unsafe void ApplyRaster(int generation, RasterKey key, uint[] buffer)
    {
        try
        {
            if (!_attached || generation != _generation)
                return; // closed, or a newer render is already on its way

            var size = key.SizePx;
            if (_bitmap is null || _bitmap.PixelSize.Width != size)
            {
                _bitmap?.Dispose();
                _bitmap = new WriteableBitmap(
                    new PixelSize(size, size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            }

            using (var fb = _bitmap.Lock())
            {
                for (var y = 0; y < size; y++)
                {
                    var row = new Span<uint>((byte*)fb.Address + (long)y * fb.RowBytes, size);
                    buffer.AsSpan(y * size, size).CopyTo(row);
                }
            }

            InvalidateVisual();
        }
        finally
        {
            ArrayPool<uint>.Shared.Return(buffer);
        }
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0)
            return;

        var palette = UiPaletteResolver.Current;
        context.FillRectangle(_renderCache.GetBrush(palette.GlobeBackground), new Rect(0, 0, w, h));

        if (Frame is not { } frame)
            return;

        var zoom = ZoomFactor;
        var side = Math.Min(w, h);
        var centre = new Point(w / 2, h / 2);
        var limbRadiusPx = GlobeRasteriser.PixelsPerEarthRadius(side, zoom, frame.Camera.AltitudeKm)
            * PerspectiveGlobeProjection.LimbRadius(frame.Camera.AltitudeKm);
        if (!double.IsFinite(limbRadiusPx) || limbRadiusPx <= 0)
            return;

        DrawGlobe(context, palette, centre, side, limbRadiusPx);
        DrawLimb(context, palette, centre, limbRadiusPx);
    }

    /// <summary>The raster stretched over the square of side <paramref name="side"/>, or a plain disc until it arrives.</summary>
    private void DrawGlobe(DrawingContext context, UiPalette palette, Point centre, double side, double limbRadiusPx)
    {
        if (_bitmap is not null)
        {
            var source = new Rect(0, 0, _bitmap.PixelSize.Width, _bitmap.PixelSize.Height);
            var destination = new Rect(centre.X - side / 2, centre.Y - side / 2, side, side);
            context.DrawImage(_bitmap, source, destination);
            return;
        }

        context.DrawEllipse(_renderCache.GetBrush(palette.GlobeSeaFallback), null, centre, limbRadiusPx, limbRadiusPx);
    }

    /// <summary>
    /// The 0 degree footprint is the edge of the visible disc, so the limb is the footprint outline. It is
    /// a thick solid line. Bright core over a dark outline, so it shows against dark ocean and bright ice.
    /// </summary>
    private void DrawLimb(DrawingContext context, UiPalette palette, Point centre, double limbRadiusPx)
    {
        context.DrawEllipse(
            null, _renderCache.GetPen(palette.MapGroundTrackOutline, LimbOutlineThickness), centre, limbRadiusPx, limbRadiusPx);
        context.DrawEllipse(
            null, _renderCache.GetPen(palette.GlobeLimbStroke, LimbCoreThickness), centre, limbRadiusPx, limbRadiusPx);
    }
}
