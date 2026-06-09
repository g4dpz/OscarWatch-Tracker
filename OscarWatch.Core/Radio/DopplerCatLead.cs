using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;

namespace OscarWatch.Core.Radio;

public static class DopplerCatLead
{
    /// <summary>
    /// CAT delay spaces serial commands; it is often much larger than actual tune latency.
    /// Cap lead so high pacing values do not over-predict Doppler for the whole pass.
    /// </summary>
    public const double MaxLeadMs = 50;

    /// <summary>Sample interval for detecting a steep range-rate leg (TCA vicinity).</summary>
    public const double RangeRateSlopeSampleSec = 1.0;

    /// <summary>
    /// Minimum |d(range rate)/dt| (km/s²) before CAT lead is applied.
    /// Gentle AOS/LOS legs stay on the snapshot rate; fast-changing TCA middle uses lead.
    /// </summary>
    public const double SteepRangeRateSlopeKmPerSec2 = 0.018;

    public static (double RxRangeRateKmPerSec, double TxRangeRateKmPerSec) ResolveRangeRates(
        IOrbitPropagator? propagator,
        RigSettings settings,
        GroundStation site,
        SatelliteTrackState state,
        DateTime utc)
    {
        var fallback = state.LookAngles?.RangeRateKmPerSec ?? 0;
        if (!settings.DopplerCatLeadEnabled
            || propagator is null
            || state.LookAngles is null
            || settings.ReceiveCatDelayMs() <= 0 && settings.TransmitCatDelayMs() <= 0)
        {
            return (fallback, fallback);
        }

        try
        {
            if (!IsSteepRangeRateLeg(propagator, state.NoradId, site, utc, fallback))
                return (fallback, fallback);

            var rxLeadMs = ResolveLeadMs(settings.ReceiveCatDelayMs());
            var txLeadMs = ResolveLeadMs(settings.TransmitCatDelayMs());
            var rxRate = rxLeadMs > 0
                ? propagator.GetLookAngles(state.NoradId, site, utc.AddMilliseconds(rxLeadMs)).RangeRateKmPerSec
                : fallback;
            var txRate = txLeadMs > 0
                ? propagator.GetLookAngles(state.NoradId, site, utc.AddMilliseconds(txLeadMs)).RangeRateKmPerSec
                : fallback;
            return (rxRate, txRate);
        }
        catch
        {
            return (fallback, fallback);
        }
    }

    internal static bool IsSteepRangeRateLeg(
        IOrbitPropagator propagator,
        string noradId,
        GroundStation site,
        DateTime utc,
        double rangeRateKmPerSec) =>
        ComputeRangeRateSlopeKmPerSec2(propagator, noradId, site, utc, rangeRateKmPerSec)
            >= SteepRangeRateSlopeKmPerSec2;

    internal static double ComputeRangeRateSlopeKmPerSec2(
        IOrbitPropagator propagator,
        string noradId,
        GroundStation site,
        DateTime utc,
        double rangeRateKmPerSec)
    {
        var ahead = propagator.GetLookAngles(
            noradId,
            site,
            utc.AddSeconds(RangeRateSlopeSampleSec));
        return Math.Abs(ahead.RangeRateKmPerSec - rangeRateKmPerSec) / RangeRateSlopeSampleSec;
    }

    internal static double ResolveLeadMs(int catDelayMs) =>
        catDelayMs <= 0 ? 0 : Math.Min(catDelayMs / 2.0, MaxLeadMs);
}
