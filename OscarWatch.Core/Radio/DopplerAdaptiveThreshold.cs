using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;

namespace OscarWatch.Core.Radio;

/// <summary>
/// Lowers the effective Doppler CAT threshold when frequency slew is fast (TCA vicinity).
/// </summary>
public static class DopplerAdaptiveThreshold
{
    /// <summary>Below this slew rate (Hz/s), use the configured threshold unchanged.</summary>
    public const double SlewStartHzPerSec = 15;

    /// <summary>At or above this slew rate (Hz/s), threshold is reduced to half (subject to minimum).</summary>
    public const double SlewFullHzPerSec = 35;

    /// <summary>Floor for the effective threshold when slew is high.</summary>
    public const int MinThresholdHz = 25;

    public static double SlewFromRangeRateSlope(double centerFreqKHz, double slopeKmPerSec2) =>
        Math.Abs(centerFreqKHz * 1000.0 * slopeKmPerSec2 / 299792.458);

    public static int Resolve(int baseThresholdHz, double slewRateHzPerSec, bool enabled)
    {
        if (!enabled || baseThresholdHz <= 0)
            return baseThresholdHz;

        if (slewRateHzPerSec <= SlewStartHzPerSec)
            return baseThresholdHz;

        var reduced = Math.Max(MinThresholdHz, baseThresholdHz / 2);
        if (slewRateHzPerSec >= SlewFullHzPerSec)
            return reduced;

        var t = (slewRateHzPerSec - SlewStartHzPerSec) / (SlewFullHzPerSec - SlewStartHzPerSec);
        return (int)Math.Round(baseThresholdHz - t * (baseThresholdHz - reduced));
    }

    public static double EstimateMaxSlewHzPerSec(
        IOrbitPropagator propagator,
        string noradId,
        GroundStation site,
        DateTime utc,
        double rangeRateKmPerSec,
        double downlinkKHz,
        double uplinkKHz,
        DopplerStrategy strategy,
        bool beaconOnly)
    {
        var slope = DopplerCatLead.ComputeRangeRateSlopeKmPerSec2(
            propagator,
            noradId,
            site,
            utc,
            rangeRateKmPerSec);

        var rxSlew = SlewFromRangeRateSlope(downlinkKHz, slope);
        if (strategy == DopplerStrategy.UplinkOnly || beaconOnly)
            return rxSlew;

        var txSlew = SlewFromRangeRateSlope(uplinkKHz, slope);
        if (strategy == DopplerStrategy.DownlinkOnly)
            return rxSlew;

        return Math.Max(rxSlew, txSlew);
    }
}
