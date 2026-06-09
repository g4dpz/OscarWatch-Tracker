using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;

namespace OscarWatch.Core.Radio;

public static class DopplerCatLead
{
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
            var rxLeadMs = settings.ReceiveCatDelayMs() / 2.0;
            var txLeadMs = settings.TransmitCatDelayMs() / 2.0;
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
}
