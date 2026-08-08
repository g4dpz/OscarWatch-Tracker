namespace OscarWatch.Core.Rotator;

/// <summary>
/// Maps compass azimuth (0–360°) to rotator command azimuth (0–max), using 361–450°
/// on extended-range rotators for shortest-path slewing over north when the track
/// is heading west. Eastbound (N→SE) tracks stay on 0–360° to avoid climbing to the
/// mechanical stop and unwinding mid-pass.
/// </summary>
public static class RotatorAzimuthPlanner
{
    /// <summary>Compass azimuths east of north that may use 361–450° before a west jump.</summary>
    private const double EastOfNorthMaxDeg = 90;

    /// <summary>Low east azimuths where extended-band descent is committed before north.</summary>
    private const double EastDescentMaxDeg = 45;

    /// <summary>
    /// Soft ceiling on extended-band commands unless a westbound north wrap is predicted.
    /// Keeps the mast off the mechanical stop on eastbound legs.
    /// </summary>
    internal const double ExtendedBandCeilingDeg = 420;

    /// <summary>
    /// When polled vs last-commanded compass azimuth differ by at least this much,
    /// prefer the polled position (operator moved the mast outside OscarWatch).
    /// </summary>
    internal const double StaleLastCompassDeltaDeg = 45;

    /// <summary>
    /// Picks the rotator command azimuth in [0, maxAzimuthDeg] that minimizes rotation
    /// from the last commanded position, without climbing the overlap band on eastbound passes.
    /// </summary>
    /// <param name="lastCommandedAzDeg">Last commanded azimuth, or null on first command after reset.</param>
    /// <param name="targetCompassAzDeg">Satellite look azimuth (compass, 0–360°).</param>
    /// <param name="maxAzimuthDeg">Rotator maximum (360 or 450).</param>
    /// <param name="nextCompassAzDeg">Optional short-horizon compass azimuth for wrap direction.</param>
    public static double ResolveCommandAz(
        double? lastCommandedAzDeg,
        double targetCompassAzDeg,
        double maxAzimuthDeg,
        double? nextCompassAzDeg = null)
    {
        var target = Normalize360(targetCompassAzDeg);
        var westboundPredicted = IsWestboundNorthWrapPredicted(target, nextCompassAzDeg);
        var eastboundSe = lastCommandedAzDeg is { } lastForDir
            && IsEastboundSeContinuation(lastForDir, target, nextCompassAzDeg);

        if (maxAzimuthDeg > 360)
        {
            // Already in overlap on an eastbound leg: unwrap early rather than climb to the stop.
            if (lastCommandedAzDeg is { } overlapLast
                && overlapLast > 360
                && eastboundSe)
            {
                return target;
            }

            if (target + 360 <= maxAzimuthDeg && !eastboundSe)
            {
                if (ShouldCommitEastSideNorthWrap(target, lastCommandedAzDeg, maxAzimuthDeg, nextCompassAzDeg))
                    return ClampExtendedUnlessWestbound(target + 360, target, westboundPredicted, maxAzimuthDeg);

                if (nextCompassAzDeg is { } next
                    && ShouldUseExtendedForImminentEastWrap(target, next, maxAzimuthDeg))
                    return ClampExtendedUnlessWestbound(target + 360, target, westboundPredicted, maxAzimuthDeg);
            }

            if (ShouldCommitWestSideNorthWrap(target, lastCommandedAzDeg, maxAzimuthDeg)
                && lastCommandedAzDeg is { } westLast)
            {
                // Westbound entry is intentional; allow the full mechanical range.
                return Math.Min(westLast + 360, maxAzimuthDeg);
            }
        }

        Span<double> candidates = stackalloc double[2];
        var count = 1;
        candidates[0] = target;
        // Eastbound SE: do not offer the overlap candidate (avoids entering 361–450°).
        if (maxAzimuthDeg > 360 && !eastboundSe && target + 360 <= maxAzimuthDeg)
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

        return ClampExtendedUnlessWestbound(best, target, westboundPredicted, maxAzimuthDeg);
    }

    /// <summary>
    /// True when compass motion is continuing east/southeast rather than wrapping west over north.
    /// </summary>
    internal static bool IsEastboundSeContinuation(
        double lastCommandedAzDeg,
        double targetCompassAzDeg,
        double? nextCompassAzDeg)
    {
        if (IsWestboundNorthWrapPredicted(targetCompassAzDeg, nextCompassAzDeg))
            return false;

        var lastCompass = Normalize360(lastCommandedAzDeg);
        var target = Normalize360(targetCompassAzDeg);

        if (nextCompassAzDeg is { } nextRaw)
        {
            var next = Normalize360(nextRaw);
            var stepEastbound = SignedCompassDeltaDeg(lastCompass, target) >= 0;
            // Lookahead through the eastern half counts when already east-of-north / in overlap,
            // or when this step itself is clockwise — not for a long CCW slew from park to AOS.
            var inEastWrapZone = lastCommandedAzDeg > 360 || lastCompass < EastOfNorthMaxDeg;
            if ((stepEastbound || inEastWrapZone) && next > target && next < 270)
                return true;
            if ((stepEastbound || inEastWrapZone)
                && target < EastDescentMaxDeg
                && next >= EastDescentMaxDeg
                && next < 270)
                return true;
        }

        if (lastCommandedAzDeg > 360)
        {
            // Climbing the overlap dial while compass az increases through NE/E/SE.
            if (target > lastCompass && target < 270)
                return true;
            // Clearly past the east-descent commit zone and heading through the east.
            if (target >= EastDescentMaxDeg && target <= 180)
                return true;
            return false;
        }

        // Not yet in overlap: clockwise compass step with eastern continuation already handled via next.
        var delta = SignedCompassDeltaDeg(lastCompass, target);
        return delta > 0 && target >= EastDescentMaxDeg && target <= 180;
    }

    /// <summary>True when the track is predicted to cross north toward the west.</summary>
    internal static bool IsWestboundNorthWrapPredicted(
        double targetCompassAzDeg,
        double? nextCompassAzDeg)
    {
        if (Normalize360(targetCompassAzDeg) > 270)
            return true;

        return nextCompassAzDeg is { } next && Normalize360(next) > 270;
    }

    /// <summary>
    /// Prefer polled mast position when it disagrees with last commanded on the compass
    /// (e.g. wind-parked outside OscarWatch while last command stayed in the overlap band).
    /// </summary>
    public static double? ResolveEffectiveLastAzimuth(
        double? lastCommandedAzDeg,
        double? polledAzimuthDeg)
    {
        if (lastCommandedAzDeg is null)
            return polledAzimuthDeg;

        if (polledAzimuthDeg is null)
            return lastCommandedAzDeg;

        var lastCompass = Normalize360(lastCommandedAzDeg.Value);
        var polledCompass = Normalize360(polledAzimuthDeg.Value);
        if (Math.Abs(SignedCompassDeltaDeg(lastCompass, polledCompass)) >= StaleLastCompassDeltaDeg)
            return polledAzimuthDeg.Value;

        return lastCommandedAzDeg.Value;
    }

    private static double ClampExtendedUnlessWestbound(
        double commandAz,
        double primaryTarget,
        bool westboundPredicted,
        double maxAzimuthDeg)
    {
        if (commandAz <= ExtendedBandCeilingDeg || westboundPredicted)
            return Math.Min(commandAz, maxAzimuthDeg);

        return primaryTarget;
    }

    /// <summary>
    /// East-of-north descent (e.g. 80° → 20° → 0° → 355°): commit to 361–450° while azimuth
    /// is still low so the post-north jump to ~355° is a short move on the extended dial.
    /// Skipped when lookahead shows eastbound SE continuation.
    /// </summary>
    internal static bool ShouldCommitEastSideNorthWrap(
        double targetCompassAzDeg,
        double? lastCommandedAzDeg,
        double maxAzimuthDeg,
        double? nextCompassAzDeg = null)
    {
        if (maxAzimuthDeg <= 360)
            return false;

        var target = Normalize360(targetCompassAzDeg);
        if (target >= EastDescentMaxDeg || target + 360 > maxAzimuthDeg)
            return false;

        if (lastCommandedAzDeg is not { } last)
            return false;

        if (nextCompassAzDeg is { } nextRaw)
        {
            var next = Normalize360(nextRaw);
            if (next > target && next < 270)
                return false;
        }

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

    /// <summary>Signed shortest compass delta from <paramref name="fromDeg"/> to <paramref name="toDeg"/> in (−180, 180].</summary>
    internal static double SignedCompassDeltaDeg(double fromDeg, double toDeg)
    {
        var delta = Normalize360(toDeg) - Normalize360(fromDeg);
        if (delta > 180)
            delta -= 360;
        else if (delta <= -180)
            delta += 360;
        return delta;
    }

    public static double Normalize360(double deg)
    {
        deg %= 360;
        if (deg < 0)
            deg += 360;
        return deg;
    }
}
