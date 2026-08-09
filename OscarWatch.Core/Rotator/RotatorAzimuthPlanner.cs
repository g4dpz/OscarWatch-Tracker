namespace OscarWatch.Core.Rotator;

/// <summary>
/// Maps compass azimuth (0–360°) to rotator command azimuth (0–max), using 361–450°
/// on extended-range rotators for shortest-path slewing over north.
/// </summary>
public static class RotatorAzimuthPlanner
{
    /// <summary>Compass azimuths east of north that may use 361–450° before a west jump.</summary>
    private const double EastOfNorthMaxDeg = 90;

    /// <summary>Low east azimuths where extended-band descent is committed before north.</summary>
    private const double EastDescentMaxDeg = 45;
    /// <summary>
    /// Picks the rotator command azimuth in [0, maxAzimuthDeg] that minimizes rotation
    /// from the last commanded position.
    /// </summary>
    /// <param name="lastCommandedAzDeg">Last commanded azimuth, or null on first command after reset.</param>
    /// <param name="targetCompassAzDeg">Satellite look azimuth (compass, 0–360°).</param>
    /// <param name="maxAzimuthDeg">Rotator maximum (360 or 450).</param>
    public static double ResolveCommandAz(
        double? lastCommandedAzDeg,
        double targetCompassAzDeg,
        double maxAzimuthDeg,
        double? nextCompassAzDeg = null)
    {
        var target = Normalize360(targetCompassAzDeg);

        if (maxAzimuthDeg > 360)
        {
            if (target + 360 <= maxAzimuthDeg)
            {
                if (ShouldCommitEastSideNorthWrap(target, lastCommandedAzDeg, maxAzimuthDeg))
                    return target + 360;

                if (nextCompassAzDeg is { } next
                    && ShouldUseExtendedForImminentEastWrap(target, next, maxAzimuthDeg))
                    return target + 360;
            }

            if (ShouldCommitWestSideNorthWrap(target, lastCommandedAzDeg, maxAzimuthDeg)
                && lastCommandedAzDeg is { } westLast)
                return westLast + 360;
        }

        Span<double> candidates = stackalloc double[2];
        var count = 1;
        candidates[0] = target;
        if (maxAzimuthDeg > 360 && target + 360 <= maxAzimuthDeg)
        {
            candidates[1] = target + 360;
            count = 2;
        }

        if (lastCommandedAzDeg is null)
            return target;

        var last = lastCommandedAzDeg.Value;
        var best = candidates[0];
        var bestDelta = Math.Abs(best - last);
        for (var i = 1; i < count; i++)
        {
            var candidate = candidates[i];
            var delta = Math.Abs(candidate - last);
            if (delta < bestDelta)
            {
                best = candidate;
                bestDelta = delta;
            }
        }

        return best;
    }

    /// <summary>
    /// East-of-north descent (e.g. 80° → 20° → 0° → 355°): commit to 361–450° while azimuth
    /// is still low so the post-north jump to ~355° is a short move on the extended dial.
    /// </summary>
    internal static bool ShouldCommitEastSideNorthWrap(
        double targetCompassAzDeg,
        double? lastCommandedAzDeg,
        double maxAzimuthDeg)
    {
        if (maxAzimuthDeg <= 360)
            return false;

        var target = Normalize360(targetCompassAzDeg);
        if (target >= EastDescentMaxDeg || target + 360 > maxAzimuthDeg)
            return false;

        if (lastCommandedAzDeg is not { } last)
            return false;

        return last < EastOfNorthMaxDeg && target <= last;
    }

    /// <summary>Compass azimuth will soon jump from east of north to west (e.g. 20° → 355°).</summary>
    internal static bool ShouldUseExtendedForImminentEastWrap(
        double targetCompassAzDeg,
        double nextCompassAzDeg,
        double maxAzimuthDeg)
    {
        if (maxAzimuthDeg <= 360)
            return false;

        var target = Normalize360(targetCompassAzDeg);
        if (target + 360 > maxAzimuthDeg)
            return false;

        var next = Normalize360(nextCompassAzDeg);
        return target < EastDescentMaxDeg && next > 270;
    }

    /// <summary>
    /// West-of-north descent after TCA (e.g. 10° → 330°): enter 361–450° from the east side
    /// so the rotator does not slew the long way through south.
    /// </summary>
    internal static bool ShouldCommitWestSideNorthWrap(
        double targetCompassAzDeg,
        double? lastCommandedAzDeg,
        double maxAzimuthDeg)
    {
        if (maxAzimuthDeg <= 360 || lastCommandedAzDeg is not { } last)
            return false;

        if (last + 360 > maxAzimuthDeg)
            return false;

        var target = Normalize360(targetCompassAzDeg);
        return last < EastOfNorthMaxDeg && target > 270;
    }

    public static double Normalize360(double deg)
    {
        deg %= 360;
        if (deg < 0)
            deg += 360;
        return deg;
    }
}
