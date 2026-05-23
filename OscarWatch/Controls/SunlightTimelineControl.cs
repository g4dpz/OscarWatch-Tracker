using Avalonia;
using Avalonia.Media;
using OscarWatch.Core.Models;

namespace OscarWatch.Controls;

/// <summary>Horizontal bar chart of sunlit vs eclipse segments over a time range.</summary>
public class SunlightTimelineControl : ThemeAwareControl
{
    public static readonly StyledProperty<IReadOnlyList<IlluminationSegment>?> SegmentsProperty =
        AvaloniaProperty.Register<SunlightTimelineControl, IReadOnlyList<IlluminationSegment>?>(nameof(Segments));

    public static readonly StyledProperty<DateTime> RangeStartUtcProperty =
        AvaloniaProperty.Register<SunlightTimelineControl, DateTime>(nameof(RangeStartUtc));

    public static readonly StyledProperty<DateTime> RangeEndUtcProperty =
        AvaloniaProperty.Register<SunlightTimelineControl, DateTime>(nameof(RangeEndUtc));

    static SunlightTimelineControl()
    {
        AffectsRender<SunlightTimelineControl>(
            SegmentsProperty,
            RangeStartUtcProperty,
            RangeEndUtcProperty);
    }

    public SunlightTimelineControl()
    {
        MinHeight = 14;
        Height = 14;
    }

    public IReadOnlyList<IlluminationSegment>? Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public DateTime RangeStartUtc
    {
        get => GetValue(RangeStartUtcProperty);
        set => SetValue(RangeStartUtcProperty, value);
    }

    public DateTime RangeEndUtc
    {
        get => GetValue(RangeEndUtcProperty);
        set => SetValue(RangeEndUtcProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
            return;

        var rangeStart = RangeStartUtc;
        var rangeEnd = RangeEndUtc;
        var totalSeconds = (rangeEnd - rangeStart).TotalSeconds;
        if (totalSeconds <= 0)
            return;

        var palette = UiPaletteResolver.Current;
        var segments = Segments;
        if (segments is null || segments.Count == 0)
        {
            context.FillRectangle(
                new SolidColorBrush(palette.EclipseTimeline),
                new Rect(0, 0, width, height));
            return;
        }

        foreach (var segment in segments)
        {
            var clipStart = segment.StartUtc < rangeStart ? rangeStart : segment.StartUtc;
            var clipEnd = segment.EndUtc > rangeEnd ? rangeEnd : segment.EndUtc;
            if (clipEnd <= clipStart)
                continue;

            var x = (clipStart - rangeStart).TotalSeconds / totalSeconds * width;
            var w = (clipEnd - clipStart).TotalSeconds / totalSeconds * width;
            if (w < 0.5)
                continue;

            var color = segment.IsSunlit ? palette.SunlightTimeline : palette.EclipseTimeline;
            context.FillRectangle(
                new SolidColorBrush(color),
                new Rect(x, 0, w, height));
        }
    }
}
