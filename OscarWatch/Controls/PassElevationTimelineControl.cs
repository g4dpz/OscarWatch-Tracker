using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Core.Services;

namespace OscarWatch.Controls;

/// <summary>
/// Custom Avalonia control that renders upcoming satellite passes as filled elevation
/// "mountain" shapes on a horizontal time axis. Integrates as a collapsible panel
/// below the world map with click-to-focus and hover tooltips.
/// </summary>
public sealed class PassElevationTimelineControl : ThemeAwareControl
{
    // --- Styled Properties ---

    public static readonly StyledProperty<IReadOnlyList<PassInfo>?> PassesProperty =
        AvaloniaProperty.Register<PassElevationTimelineControl, IReadOnlyList<PassInfo>?>(nameof(Passes));

    public static readonly StyledProperty<int> TimeWindowMinutesProperty =
        AvaloniaProperty.Register<PassElevationTimelineControl, int>(nameof(TimeWindowMinutes), 120);

    public static readonly StyledProperty<string?> FocusedNoradIdProperty =
        AvaloniaProperty.Register<PassElevationTimelineControl, string?>(nameof(FocusedNoradId));

    static PassElevationTimelineControl()
    {
        AffectsRender<PassElevationTimelineControl>(PassesProperty, TimeWindowMinutesProperty);
        ClipToBoundsProperty.OverrideDefaultValue<PassElevationTimelineControl>(true);
        MinHeightProperty.OverrideDefaultValue<PassElevationTimelineControl>(80);
    }

    public PassElevationTimelineControl()
    {
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _refreshTimer.Tick += (_, _) => InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _refreshTimer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _refreshTimer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    // --- Events ---

    /// <summary>
    /// Raised when the operator clicks on a mountain shape to focus a satellite.
    /// The event argument is the NORAD ID of the selected satellite.
    /// </summary>
    public event EventHandler<string>? SatelliteFocusRequested;

    // --- Render caches ---

    private readonly RenderResourceCache _renderCache = new();
    private readonly FormattedTextCache _labelCache = new();
    private readonly DispatcherTimer _refreshTimer;

    // --- Cached pass geometry ---

    private Dictionary<string, TimelinePassEntry> _passEntries = new();

    // --- Orbit propagator (injected via property for integration) ---
    private IOrbitPropagator? _propagator;
    private GroundStation? _site;

    /// <summary>
    /// Sets the propagator and site used for elevation profile computation.
    /// Call this after the control is created and services are available.
    /// </summary>
    public void SetPropagator(IOrbitPropagator? propagator, GroundStation? site)
    {
        _propagator = propagator;
        _site = site;
    }

    // --- Properties ---

    public IReadOnlyList<PassInfo>? Passes
    {
        get => GetValue(PassesProperty);
        set => SetValue(PassesProperty, value);
    }

    public int TimeWindowMinutes
    {
        get => GetValue(TimeWindowMinutesProperty);
        set => SetValue(TimeWindowMinutesProperty, value);
    }

    public string? FocusedNoradId
    {
        get => GetValue(FocusedNoradIdProperty);
        set => SetValue(FocusedNoradIdProperty, value);
    }

    // --- Render ---

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PassesProperty)
        {
            RecomputeProfiles();
        }
        else if (change.Property == BoundsProperty)
        {
            InvalidateGeometryCache();
        }
    }

    /// <summary>
    /// Recomputes elevation profiles for all passes asynchronously.
    /// </summary>
    private void RecomputeProfiles()
    {
        var passes = Passes;
        var propagator = _propagator;
        var site = _site;

        if (passes is null || passes.Count == 0)
        {
            _passEntries.Clear();
            InvalidateVisual();
            return;
        }

        var now = DateTime.UtcNow;
        var sampleInterval = TimeSpan.FromSeconds(30);

        // Compute profiles on background thread
        _ = Task.Run(() =>
        {
            var entries = new Dictionary<string, TimelinePassEntry>();
            for (var i = 0; i < passes.Count; i++)
            {
                var pass = passes[i];
                var key = $"{pass.NoradId}_{pass.AosUtc.Ticks}";

                IReadOnlyList<ElevationSample> profile;
                if (propagator is not null && site is not null && propagator.HasSatellite(pass.NoradId))
                {
                    try
                    {
                        profile = ElevationProfileBuilder.Build(pass, propagator, site, sampleInterval, now);
                    }
                    catch
                    {
                        profile = BuildFallbackProfile(pass, now);
                    }
                }
                else
                {
                    profile = BuildFallbackProfile(pass, now);
                }

                entries[key] = new TimelinePassEntry
                {
                    Pass = pass,
                    Profile = profile,
                    PaletteIndex = i,
                };
            }

            Dispatcher.UIThread.Post(() =>
            {
                _passEntries = entries;
                InvalidateGeometryCache();
                InvalidateVisual();
            });
        });
    }

    /// <summary>
    /// Builds a simple triangular fallback profile when the propagator is unavailable.
    /// Uses AOS=0°, peak=MaxElevation, LOS=0°.
    /// </summary>
    private static IReadOnlyList<ElevationSample> BuildFallbackProfile(PassInfo pass, DateTime referenceUtc)
    {
        return new[]
        {
            new ElevationSample((pass.AosUtc - referenceUtc).TotalMinutes, 0.0),
            new ElevationSample((pass.MaxElevationUtc - referenceUtc).TotalMinutes, pass.MaxElevationDeg),
            new ElevationSample((pass.LosUtc - referenceUtc).TotalMinutes, 0.0),
        };
    }

    /// <summary>
    /// Invalidates all cached StreamGeometry objects, forcing rebuild on next render.
    /// </summary>
    private void InvalidateGeometryCache()
    {
        foreach (var entry in _passEntries.Values)
        {
            entry.Geometry = null;
        }
    }

    /// <summary>
    /// Builds or returns the cached StreamGeometry for a pass entry.
    /// </summary>
    internal static StreamGeometry BuildGeometry(
        TimelinePassEntry entry,
        double width,
        double height,
        double windowMinutes,
        DateTime now)
    {
        if (entry.Geometry is not null
            && Math.Abs(entry.GeometryWidth - width) < 0.5
            && Math.Abs(entry.GeometryHeight - height) < 0.5)
        {
            return entry.Geometry;
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var profile = entry.Profile;
            if (profile.Count == 0)
            {
                entry.Geometry = geometry;
                entry.GeometryWidth = width;
                entry.GeometryHeight = height;
                return geometry;
            }

            var baselineY = height;

            // Start at baseline at first sample X
            var x0 = TimeToX(profile[0].MinutesFromNow, width, windowMinutes);
            ctx.BeginFigure(new Point(x0, baselineY), true);

            // Follow elevation samples
            for (var i = 0; i < profile.Count; i++)
            {
                var sample = profile[i];
                var x = TimeToX(sample.MinutesFromNow, width, windowMinutes);
                var y = ElevToY(sample.ElevationDeg, height);
                ctx.LineTo(new Point(x, y));
            }

            // Close back to baseline at last sample X
            var xN = TimeToX(profile[^1].MinutesFromNow, width, windowMinutes);
            ctx.LineTo(new Point(xN, baselineY));
            ctx.EndFigure(true);
        }

        entry.Geometry = geometry;
        entry.GeometryWidth = width;
        entry.GeometryHeight = height;
        return geometry;
    }

    // --- Coordinate mapping (exposed as internal static for testing) ---

    /// <summary>
    /// Maps a time value (minutes from now) to an X pixel coordinate.
    /// </summary>
    internal static double TimeToX(double minutesFromNow, double width, double windowMinutes)
        => minutesFromNow / windowMinutes * width;

    /// <summary>
    /// Maps an elevation angle (0–90°) to a Y pixel coordinate.
    /// 0° is at the bottom (Y = height), 90° is at the top (Y = 0).
    /// </summary>
    internal static double ElevToY(double elevDeg, double height)
        => height - (elevDeg / 90.0) * height;

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0)
            return;

        var palette = UiPaletteResolver.Current;
        context.FillRectangle(
            _renderCache.GetBrush(palette.SkyPlotBackground),
            new Rect(0, 0, w, h));

        var windowMinutes = Math.Clamp(TimeWindowMinutes, 30, 360);

        // --- Grid lines ---
        DrawGrid(context, w, h, windowMinutes, palette);

        // --- Mountain shapes ---
        DrawMountains(context, w, h, windowMinutes, palette);

        // --- Now indicator ---
        DrawNowIndicator(context, h, palette);
    }

    private void DrawGrid(DrawingContext context, double w, double h, int windowMinutes, UiPalette palette)
    {
        var gridColor = Color.FromArgb(64, palette.SkyPlotBorder.R, palette.SkyPlotBorder.G, palette.SkyPlotBorder.B);
        var gridPen = _renderCache.GetPen(gridColor, 1);

        // Vertical grid lines at 30-minute intervals
        var intervalMinutes = 30;
        for (var mins = intervalMinutes; mins < windowMinutes; mins += intervalMinutes)
        {
            var x = (double)mins / windowMinutes * w;
            context.DrawLine(gridPen, new Point(x, 0), new Point(x, h));

            // Time label
            var label = mins.ToString("D3");
            var text = _labelCache.Get(label, 9, palette);
            context.DrawText(text, new Point(x + 2, h - text.Height - 2));
        }

        // "000" label at left
        var zeroLabel = _labelCache.Get("000", 9, palette);
        context.DrawText(zeroLabel, new Point(2, h - zeroLabel.Height - 2));

        // Horizontal reference line at 45° elevation
        var refY = ElevToY(45, h);
        var refColor = Color.FromArgb(64, palette.SkyPlotBorder.R, palette.SkyPlotBorder.G, palette.SkyPlotBorder.B);
        var refPen = _renderCache.GetDashedPen(refColor, 1);
        context.DrawLine(refPen, new Point(0, refY), new Point(w, refY));
    }

    private void DrawMountains(DrawingContext context, double w, double h, int windowMinutes, UiPalette palette)
    {
        if (_passEntries.Count == 0)
            return;

        var now = DateTime.UtcNow;

        // Sort by AOS (earlier passes render first / behind)
        var sorted = _passEntries.Values
            .OrderBy(e => e.Pass.AosUtc)
            .ToList();

        for (var i = 0; i < sorted.Count; i++)
        {
            var entry = sorted[i];
            var profile = entry.Profile;
            if (profile.Count < 2)
                continue;

            // Check if any part is within the visible window
            var firstMin = profile[0].MinutesFromNow;
            var lastMin = profile[^1].MinutesFromNow;

            // Recompute minutes from current now (profiles were built at a reference time)
            var passAosMinutes = (entry.Pass.AosUtc - now).TotalMinutes;
            var passLosMinutes = (entry.Pass.LosUtc - now).TotalMinutes;

            if (passLosMinutes < 0 || passAosMinutes > windowMinutes)
                continue;

            // Build geometry using profile's stored minutes-from-reference
            // We need to rebuild with current time reference for scrolling
            var currentProfile = new List<ElevationSample>();
            foreach (var sample in profile)
            {
                // Adjust: original MinutesFromNow was relative to computation time
                // We need to use pass-relative offsets and recompute from current now
                var sampleUtc = entry.Pass.AosUtc + TimeSpan.FromMinutes(
                    (sample.MinutesFromNow - profile[0].MinutesFromNow));
                var minutesFromNow = (sampleUtc - now).TotalMinutes;
                currentProfile.Add(new ElevationSample(minutesFromNow, sample.ElevationDeg));
            }

            // Build inline geometry for this render (using current time reference)
            var geo = BuildInlineGeometry(currentProfile, w, h, windowMinutes);

            // Opacity fade: 128 for passes at now, fading to 64 for passes at end of window
            var distanceFraction = Math.Clamp(passAosMinutes / windowMinutes, 0, 1);
            var fillOpacity = (byte)(128 - (int)(64 * distanceFraction));

            var satColor = PlotColors.ForIndex(entry.PaletteIndex);
            var fillColor = Color.FromArgb(fillOpacity, satColor.R, satColor.G, satColor.B);
            var strokeColor = Color.FromArgb(255, satColor.R, satColor.G, satColor.B);

            var fillBrush = _renderCache.GetBrush(fillColor);
            var strokePen = _renderCache.GetPen(strokeColor, 1);

            // Clip at left edge for in-progress passes
            using (context.PushClip(new Rect(0, 0, w, h)))
            {
                context.DrawGeometry(fillBrush, strokePen, geo);
            }

            // Peak label: satellite name above peak
            DrawPeakLabel(context, entry, currentProfile, w, h, windowMinutes, palette);
        }
    }

    private static StreamGeometry BuildInlineGeometry(
        List<ElevationSample> profile,
        double width,
        double height,
        double windowMinutes)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            if (profile.Count == 0)
                return geometry;

            var baselineY = height;
            var x0 = TimeToX(profile[0].MinutesFromNow, width, windowMinutes);
            ctx.BeginFigure(new Point(x0, baselineY), true);

            for (var i = 0; i < profile.Count; i++)
            {
                var sample = profile[i];
                var x = TimeToX(sample.MinutesFromNow, width, windowMinutes);
                var y = ElevToY(sample.ElevationDeg, height);
                ctx.LineTo(new Point(x, y));
            }

            var xN = TimeToX(profile[^1].MinutesFromNow, width, windowMinutes);
            ctx.LineTo(new Point(xN, baselineY));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private void DrawPeakLabel(
        DrawingContext context,
        TimelinePassEntry entry,
        List<ElevationSample> profile,
        double w,
        double h,
        double windowMinutes,
        UiPalette palette)
    {
        // Find peak sample
        var peakSample = profile[0];
        for (var i = 1; i < profile.Count; i++)
        {
            if (profile[i].ElevationDeg > peakSample.ElevationDeg)
                peakSample = profile[i];
        }

        var peakX = TimeToX(peakSample.MinutesFromNow, w, windowMinutes);
        var peakY = ElevToY(peakSample.ElevationDeg, h);

        // Only draw if peak is within visible area
        if (peakX < 0 || peakX > w)
            return;

        var text = _labelCache.Get(entry.Pass.SatelliteName, 9, palette);
        var labelX = peakX - text.Width / 2;
        var labelY = peakY - text.Height - 2;

        // Clamp to visible area
        labelX = Math.Clamp(labelX, 0, Math.Max(0, w - text.Width));
        labelY = Math.Max(0, labelY);

        context.DrawText(text, new Point(labelX, labelY));
    }

    private void DrawNowIndicator(DrawingContext context, double h, UiPalette palette)
    {
        var nowColor = Color.FromArgb(180, palette.SkyPlotLabel.R, palette.SkyPlotLabel.G, palette.SkyPlotLabel.B);
        var nowPen = _renderCache.GetPen(nowColor, 2);
        context.DrawLine(nowPen, new Point(0, 0), new Point(0, h));
    }

    // --- Pointer interaction ---

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var pos = e.GetPosition(this);
        var hit = HitTest(pos.X);
        if (hit is not null)
        {
            RaiseSatelliteFocusRequested(hit.NoradId);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var pos = e.GetPosition(this);
        var hit = HitTest(pos.X);
        if (hit is not null)
        {
            var duration = hit.Duration;
            var tip = $"{hit.SatelliteName}\n" +
                      $"AOS: {hit.AosUtc:HH:mm:ss} UTC\n" +
                      $"LOS: {hit.LosUtc:HH:mm:ss} UTC\n" +
                      $"Max El: {hit.MaxElevationDeg:F1}°\n" +
                      $"Duration: {duration.Minutes}m {duration.Seconds}s";
            ToolTip.SetTip(this, tip);
            ToolTip.SetIsOpen(this, true);
        }
        else
        {
            ToolTip.SetIsOpen(this, false);
            ToolTip.SetTip(this, null);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ToolTip.SetIsOpen(this, false);
        ToolTip.SetTip(this, null);
    }

    /// <summary>
    /// Performs hit testing at the given X coordinate. Returns the pass with
    /// the highest elevation at the clicked time, or null if no pass is there.
    /// </summary>
    internal PassInfo? HitTest(double clickX)
    {
        var w = Bounds.Width;
        if (w <= 0 || _passEntries.Count == 0)
            return null;

        var windowMinutes = Math.Clamp(TimeWindowMinutes, 30, 360);
        var clickMinutes = clickX / w * windowMinutes;
        var now = DateTime.UtcNow;

        PassInfo? bestPass = null;
        double bestElev = -1;

        foreach (var entry in _passEntries.Values)
        {
            var pass = entry.Pass;
            var passAosMin = (pass.AosUtc - now).TotalMinutes;
            var passLosMin = (pass.LosUtc - now).TotalMinutes;

            if (clickMinutes < passAosMin || clickMinutes > passLosMin)
                continue;

            // Interpolate elevation at click time from the profile
            var elev = InterpolateElevation(entry.Profile, clickMinutes, pass, now);
            if (elev > 0 && elev > bestElev)
            {
                bestElev = elev;
                bestPass = pass;
            }
        }

        return bestPass;
    }

    /// <summary>
    /// Interpolates the elevation at a given minutes-from-now value using the stored profile.
    /// </summary>
    internal static double InterpolateElevation(
        IReadOnlyList<ElevationSample> profile,
        double targetMinutes,
        PassInfo pass,
        DateTime now)
    {
        if (profile.Count == 0)
            return 0;

        // Convert profile samples to current-time-relative minutes
        var firstSampleMin = (pass.AosUtc - now).TotalMinutes;
        var lastSampleMin = (pass.LosUtc - now).TotalMinutes;

        if (targetMinutes < firstSampleMin || targetMinutes > lastSampleMin)
            return 0;

        // Linear interpolation between profile points
        for (var i = 0; i < profile.Count - 1; i++)
        {
            var sampleUtcI = pass.AosUtc + TimeSpan.FromMinutes(
                profile[i].MinutesFromNow - profile[0].MinutesFromNow);
            var sampleUtcNext = pass.AosUtc + TimeSpan.FromMinutes(
                profile[i + 1].MinutesFromNow - profile[0].MinutesFromNow);

            var minI = (sampleUtcI - now).TotalMinutes;
            var minNext = (sampleUtcNext - now).TotalMinutes;

            if (targetMinutes >= minI && targetMinutes <= minNext)
            {
                var t = (minNext - minI) > 0
                    ? (targetMinutes - minI) / (minNext - minI)
                    : 0;
                return profile[i].ElevationDeg + t * (profile[i + 1].ElevationDeg - profile[i].ElevationDeg);
            }
        }

        return 0;
    }

    // --- Internal helpers (used by later tasks) ---

    internal void RaiseSatelliteFocusRequested(string noradId)
        => SatelliteFocusRequested?.Invoke(this, noradId);

    // --- Accessibility ---

    protected override AutomationPeer OnCreateAutomationPeer()
        => new PassElevationTimelineAutomationPeer(this);

    /// <summary>
    /// Returns a summary of visible passes for screen reader access.
    /// </summary>
    internal string GetAccessiblePassSummary()
    {
        if (_passEntries.Count == 0)
            return "No upcoming passes";

        var now = DateTime.UtcNow;
        var windowMinutes = Math.Clamp(TimeWindowMinutes, 30, 360);
        var visible = _passEntries.Values
            .Where(e => (e.Pass.LosUtc - now).TotalMinutes > 0 && (e.Pass.AosUtc - now).TotalMinutes < windowMinutes)
            .OrderBy(e => e.Pass.AosUtc)
            .Take(10)
            .ToList();

        if (visible.Count == 0)
            return "No upcoming passes";

        var parts = visible.Select(e =>
        {
            var p = e.Pass;
            return $"{p.SatelliteName}: {p.AosUtc:HH:mm}-{p.LosUtc:HH:mm} UTC, max {p.MaxElevationDeg:F0}°";
        });

        return $"{visible.Count} passes: " + string.Join("; ", parts);
    }
}

internal sealed class PassElevationTimelineAutomationPeer : ControlAutomationPeer
{
    public PassElevationTimelineAutomationPeer(PassElevationTimelineControl owner)
        : base(owner)
    {
    }

    protected override string GetNameCore()
    {
        if (Owner is PassElevationTimelineControl ctrl)
            return ctrl.GetAccessiblePassSummary();
        return "Pass Elevation Timeline";
    }

    protected override AutomationControlType GetAutomationControlTypeCore()
        => AutomationControlType.Custom;
}

/// <summary>
/// Internal data class holding cached geometry and profile data for a single pass
/// in the elevation timeline.
/// </summary>
internal sealed class TimelinePassEntry
{
    public required PassInfo Pass { get; init; }
    public IReadOnlyList<ElevationSample> Profile { get; set; } = [];
    public StreamGeometry? Geometry { get; set; }
    public double GeometryWidth { get; set; }
    public double GeometryHeight { get; set; }
    public int PaletteIndex { get; set; }
}
