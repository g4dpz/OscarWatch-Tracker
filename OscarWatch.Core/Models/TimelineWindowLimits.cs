namespace OscarWatch.Core.Models;

/// <summary>
/// Lookahead window for the pass elevation timeline (minutes). Scroll-wheel zoom
/// steps through <see cref="ZoomStepsMinutes"/> and stays within
/// <see cref="MinMinutes"/>–<see cref="MaxMinutes"/>.
/// </summary>
public static class TimelineWindowLimits
{
    public const int MinMinutes = 30;
    public const int MaxMinutes = 360;
    public const int DefaultMinutes = 120;

    /// <summary>
    /// Discrete zoom steps from shortest (zoomed in) to longest (zoomed out).
    /// </summary>
    public static readonly int[] ZoomStepsMinutes = [30, 45, 60, 90, 120, 180, 240, 360];

    public static int Clamp(int minutes) => Math.Clamp(minutes, MinMinutes, MaxMinutes);

    /// <summary>
    /// Moves one zoom step. Positive <paramref name="zoomInDirection"/> shortens the
    /// window; negative lengthens it. Values that are not on a step snap to the next
    /// step in that direction.
    /// </summary>
    public static int Zoom(int currentMinutes, int zoomInDirection)
    {
        var current = Clamp(currentMinutes);
        if (zoomInDirection > 0)
        {
            for (var i = ZoomStepsMinutes.Length - 1; i >= 0; i--)
            {
                if (ZoomStepsMinutes[i] < current)
                    return ZoomStepsMinutes[i];
            }

            return MinMinutes;
        }

        if (zoomInDirection < 0)
        {
            for (var i = 0; i < ZoomStepsMinutes.Length; i++)
            {
                if (ZoomStepsMinutes[i] > current)
                    return ZoomStepsMinutes[i];
            }

            return MaxMinutes;
        }

        return current;
    }
}
