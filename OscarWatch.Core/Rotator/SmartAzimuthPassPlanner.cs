using OscarWatch.Core.Models;

namespace OscarWatch.Core.Rotator;

/// <summary>
/// Builds an AOS–LOS Smart 450° band plan by shortest-path search over a pass profile.
/// </summary>
public static class SmartAzimuthPassPlanner
{
    /// <summary>
    /// Chooses Primary vs Extended band for each profile sample to minimise total dial travel
    /// from <paramref name="startCommandAzDeg"/>.
    /// </summary>
    /// <param name="profile">Full or remaining pass samples (1 Hz).</param>
    /// <param name="maxAzimuthDeg">Rotator maximum (must be &gt; 360 for Extended to be used).</param>
    /// <param name="startCommandAzDeg">Mast position on the command dial at plan start (park / last / polled).</param>
    public static SmartAzimuthPassPlan? Analyse(
        PassProfile profile,
        double maxAzimuthDeg,
        double startCommandAzDeg)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.Points.Count == 0 || maxAzimuthDeg <= 360)
            return null;

        var n = profile.Points.Count;
        var primaryCmd = new double[n];
        var extendedCmd = new double[n];
        var extendedOk = new bool[n];

        for (var i = 0; i < n; i++)
        {
            var az = RotatorAzimuthPlanner.Normalize360(profile.Points[i].AzimuthDeg);
            primaryCmd[i] = az;
            var ext = az + 360;
            if (ext <= maxAzimuthDeg)
            {
                extendedCmd[i] = ext;
                extendedOk[i] = true;
            }
        }

        // cost[i, band], prevBand[i, band]
        var cost = new double[n, 2];
        var prev = new int[n, 2];
        const double Inf = double.PositiveInfinity;

        for (var band = 0; band < 2; band++)
        {
            if (band == (int)SmartAzimuthBand.Extended && !extendedOk[0])
            {
                cost[0, band] = Inf;
                prev[0, band] = -1;
                continue;
            }

            var cmd = band == (int)SmartAzimuthBand.Extended ? extendedCmd[0] : primaryCmd[0];
            cost[0, band] = Math.Abs(cmd - startCommandAzDeg);
            prev[0, band] = -1;
        }

        for (var i = 1; i < n; i++)
        {
            for (var band = 0; band < 2; band++)
            {
                cost[i, band] = Inf;
                prev[i, band] = -1;

                if (band == (int)SmartAzimuthBand.Extended && !extendedOk[i])
                    continue;

                var cmd = band == (int)SmartAzimuthBand.Extended ? extendedCmd[i] : primaryCmd[i];
                for (var prevBand = 0; prevBand < 2; prevBand++)
                {
                    if (double.IsPositiveInfinity(cost[i - 1, prevBand]))
                        continue;

                    var prevCmd = prevBand == (int)SmartAzimuthBand.Extended
                        ? extendedCmd[i - 1]
                        : primaryCmd[i - 1];
                    var edge = Math.Abs(cmd - prevCmd);
                    // Forbid catastrophic mid-pass dial jumps (e.g. Extended climb then unwrap).
                    // Start→first-sample cost is uncapped so one early unwrap remains allowed.
                    if (edge > 180)
                        continue;

                    var total = cost[i - 1, prevBand] + edge;
                    if (total < cost[i, band])
                    {
                        cost[i, band] = total;
                        prev[i, band] = prevBand;
                    }
                }
            }
        }

        var endBand = 0;
        if (cost[n - 1, 1] < cost[n - 1, 0])
            endBand = 1;

        if (double.IsPositiveInfinity(cost[n - 1, endBand]))
            return null;

        var bands = new SmartAzimuthBand[n];
        var b = endBand;
        for (var i = n - 1; i >= 0; i--)
        {
            bands[i] = (SmartAzimuthBand)b;
            b = prev[i, b];
            if (i > 0 && b < 0)
                return null;
        }

        var samples = new SmartAzimuthPassSample[n];
        for (var i = 0; i < n; i++)
            samples[i] = new SmartAzimuthPassSample(profile.Points[i].Utc, bands[i]);

        return new SmartAzimuthPassPlan(
            profile.Pass.AosUtc,
            profile.Pass.LosUtc,
            samples);
    }

    /// <summary>
    /// Returns the planned band for <paramref name="utc"/>, or null if outside the plan window
    /// or the plan has no samples.
    /// </summary>
    public static SmartAzimuthBand? LookupBand(SmartAzimuthPassPlan? plan, DateTime utc)
    {
        if (plan is null || plan.Samples.Count == 0)
            return null;

        if (utc < plan.AosUtc || utc > plan.LosUtc)
            return null;

        // Last sample with Utc <= now; if before first sample but still in window, use first.
        var samples = plan.Samples;
        if (utc <= samples[0].Utc)
            return samples[0].Band;

        for (var i = samples.Count - 1; i >= 0; i--)
        {
            if (samples[i].Utc <= utc)
                return samples[i].Band;
        }

        return samples[^1].Band;
    }

    /// <summary>
    /// Builds a profile suffix from <paramref name="fromUtc"/> onward for mid-pass join planning.
    /// </summary>
    public static PassProfile? SliceFrom(PassProfile profile, DateTime fromUtc)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.Points.Count == 0)
            return null;

        var startIndex = 0;
        for (var i = 0; i < profile.Points.Count; i++)
        {
            if (profile.Points[i].Utc >= fromUtc)
            {
                startIndex = i;
                break;
            }

            startIndex = i;
        }

        if (startIndex == 0 && profile.Points[0].Utc < fromUtc)
        {
            // All points before fromUtc: keep last point so Analyse has something.
            startIndex = profile.Points.Count - 1;
        }

        if (startIndex == 0)
            return profile;

        var sliced = new List<PassProfilePoint>(profile.Points.Count - startIndex);
        for (var i = startIndex; i < profile.Points.Count; i++)
            sliced.Add(profile.Points[i]);

        return sliced.Count == 0 ? null : new PassProfile(profile.Pass, sliced);
    }
}
