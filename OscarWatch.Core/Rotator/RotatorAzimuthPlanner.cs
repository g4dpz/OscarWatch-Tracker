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
    /// <param name="nextCompassAzDeg">Compass azimuth a few seconds ahead, when known.</param>
    /// <param name="remainingPathCrossesNorth">
    /// True when the rest of this pass will jump east-of-north to west-of-north (cross 0°).
    /// Without this, a northbound pass that turns around before 0° must stay in the primary band.
    /// </param>
    public static double ResolveCommandAz(
        double? lastCommandedAzDeg,
        double targetCompassAzDeg,
        double maxAzimuthDeg,
        double? nextCompassAzDeg = null,
        bool remainingPathCrossesNorth = false)
    {
        var target = Normalize360(targetCompassAzDeg);

        if (maxAzimuthDeg > 360)
        {
            if (target + 360 <= maxAzimuthDeg)
            {
                if (ShouldCommitEastSideNorthWrap(target, maxAzimuthDeg, remainingPathCrossesNorth))
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
    /// Only when the remaining path will actually cross 0°. A northbound pass that bottoms
    /// out east of north (e.g. RS-44 heading north, LOS still ~20°) must not unwind to 400°.
    /// First command (no last dial position) still commits: AOS already in the descent zone
    /// (e.g. FO-29 rising near 20° then wrapping west) must start on 361–450°, not 20°.
    /// </summary>
    internal static bool ShouldCommitEastSideNorthWrap(
        double targetCompassAzDeg,
        double maxAzimuthDeg,
        bool remainingPathCrossesNorth = false)
    {
        if (maxAzimuthDeg <= 360 || !remainingPathCrossesNorth)
            return false;

        var target = Normalize360(targetCompassAzDeg);
        if (target >= EastDescentMaxDeg || target + 360 > maxAzimuthDeg)
            return false;

        return true;
    }

    /// <summary>
    /// True when the sky path from <paramref name="fromCompassAzDeg"/> to
    /// <paramref name="toCompassAzDeg"/> jumps east-of-north to west-of-north (crosses 0°).
    /// </summary>
    public static bool IndicatesEastToWestNorthCrossing(double fromCompassAzDeg, double toCompassAzDeg)
    {
        var from = Normalize360(fromCompassAzDeg);
        var to = Normalize360(toCompassAzDeg);
        return from < EastOfNorthMaxDeg && to > 270;
    }

    /// <summary>
    /// True when the rest of this pass will wrap east-of-north through 0° toward the west.
    /// LOS in the NW quadrant (above 270°) is enough. LOS further west or south (90–270°)
    /// also wraps when azimuth is descending toward north, so a FO-29-class pass
    /// (AOS ~20°, LOS ~232°) uses 361–450° instead of the primary 20° stop.
    /// Eastbound passes through south (azimuth increasing) stay in the primary band.
    /// </summary>
    public static bool RemainingPathCrossesNorthEastToWest(
        double currentCompassAzDeg,
        double losCompassAzDeg,
        double? nextCompassAzDeg = null)
    {
        var current = Normalize360(currentCompassAzDeg);
        var los = Normalize360(losCompassAzDeg);

        if (IndicatesEastToWestNorthCrossing(current, los))
            return true;

        if (nextCompassAzDeg is { } next)
        {
            next = Normalize360(next);
            if (IndicatesEastToWestNorthCrossing(current, next))
                return true;

            // Still east of north, heading toward 0°, and LOS is not still east of north:
            // the only way to reach LOS is through the north wrap.
            if (current < EastOfNorthMaxDeg
                && los >= EastOfNorthMaxDeg
                && next < current
                && next < EastOfNorthMaxDeg)
                return true;
        }

        return false;
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

    /// <summary>
    /// Shortest compass separation in degrees, treating 0/360 and 450-overlap
    /// headings (for example 15° and 375°) as the same direction.
    /// </summary>
    public static double CompassSeparationDeg(double firstDeg, double secondDeg)
    {
        var delta = Math.Abs(Normalize360(firstDeg) - Normalize360(secondDeg));
        return Math.Min(delta, 360 - delta);
    }

    /// <summary>
    /// True when two azimuth readings are closer than <paramref name="thresholdDeg"/>.
    /// Used for arrival checks so overlap-band feedback (15° vs 375°) is not treated as 360° off.
    /// </summary>
    public static bool IsWithinAzimuthThreshold(double firstDeg, double secondDeg, double thresholdDeg)
    {
        if (Math.Abs(firstDeg - secondDeg) < thresholdDeg)
            return true;

        return CompassSeparationDeg(firstDeg, secondDeg) < thresholdDeg;
    }
}
