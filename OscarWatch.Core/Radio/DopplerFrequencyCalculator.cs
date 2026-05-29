using OscarWatch.Core.Models;

namespace OscarWatch.Core.Radio;

public static class DopplerFrequencyCalculator
{
    private const double SpeedOfLightKmPerSec = 299792.458;
    internal const double RangeRateProbeDeltaSec = 0.1;

    /// <summary>Short-window range rate (km/s) from slant range now vs at <paramref name="utc"/> + probe interval.</summary>
    public static double ShortWindowRangeRateKmPerSec(double rangeNowKm, double rangeAtProbeKm) =>
        (rangeAtProbeKm - rangeNowKm) / RangeRateProbeDeltaSec;

    /// <summary>
    /// Applies the same doppler formula to downlink and uplink for NOR and REV.
    /// REV passband coupling is handled outside this type (Main dial mutates passband baselines).
    /// Receive offset and passband trim adjust satellite nominals; doppler is applied on the radio row only.
    /// </summary>
    public static CorrectedFrequencies Compute(
        SatelliteTransponderMode mode,
        double rangeRateKmPerSec,
        double receiveOffsetKHz,
        double passbandDownlinkAdjustKHz = 0,
        double passbandUplinkAdjustKHz = 0,
        DopplerComputeOptions? options = null)
    {
        if (options is { PredictiveLinear: true }
            && SetupVfosPolicy.IsLinearMode(mode.DownlinkMode))
        {
            rangeRateKmPerSec = PredictRangeRateKmPerSec(
                mode,
                rangeRateKmPerSec,
                options.Value.RangeRateProbeKmPerSec,
                receiveOffsetKHz,
                passbandDownlinkAdjustKHz,
                passbandUplinkAdjustKHz);
        }

        var isBeaconOnly = mode.IsBeaconOnly;

        var downlinkBase = mode.DownlinkKHz + receiveOffsetKHz + passbandDownlinkAdjustKHz;
        var uplinkBase = mode.UplinkKHz + passbandUplinkAdjustKHz;

        var shiftDown = ComputeShiftKHz(downlinkBase, rangeRateKmPerSec);
        var shiftUp = isBeaconOnly ? 0 : ComputeShiftKHz(uplinkBase, rangeRateKmPerSec);

        var radioRx = downlinkBase + shiftDown;
        var radioTx = isBeaconOnly ? 0 : uplinkBase - shiftUp;

        return new CorrectedFrequencies(
            RadioTransmitKHz: radioTx,
            RadioReceiveKHz: radioRx,
            SatelliteTransmitKHz: uplinkBase,
            SatelliteReceiveKHz: downlinkBase,
            DopplerShiftKHz: shiftDown,
            IsBeaconOnly: isBeaconOnly);
    }

    /// <summary>Combined RX+TX Doppler correction rate (Hz/s) from now vs short look-ahead range rate.</summary>
    public static double EstimateCombinedDopplerRateHzPerSec(
        SatelliteTransponderMode mode,
        double rangeRateNowKmPerSec,
        double rangeRateProbeKmPerSec,
        double receiveOffsetKHz,
        double passbandDownlinkAdjustKHz = 0,
        double passbandUplinkAdjustKHz = 0)
    {
        var now = Compute(
            mode,
            rangeRateNowKmPerSec,
            receiveOffsetKHz,
            passbandDownlinkAdjustKHz,
            passbandUplinkAdjustKHz);

        var probe = Compute(
            mode,
            rangeRateProbeKmPerSec,
            receiveOffsetKHz,
            passbandDownlinkAdjustKHz,
            passbandUplinkAdjustKHz);

        var rxRate = Math.Abs(now.RadioReceiveKHz - probe.RadioReceiveKHz) * 1000.0 / RangeRateProbeDeltaSec;
        if (mode.IsBeaconOnly)
            return rxRate;

        var txRate = Math.Abs(now.RadioTransmitKHz - probe.RadioTransmitKHz) * 1000.0 / RangeRateProbeDeltaSec;
        return rxRate + txRate;
    }

    /// <summary>Lowers the linear CAT threshold when correction is changing quickly (e.g. near TCA).</summary>
    public static int AdaptiveLinearThresholdHz(int configuredThresholdHz, double combinedDopplerRateHzPerSec)
    {
        if (configuredThresholdHz <= 0)
            return 0;

        return combinedDopplerRateHzPerSec switch
        {
            > 2500 => Math.Max(50, configuredThresholdHz / 2),
            > 1500 => Math.Max(75, configuredThresholdHz * 2 / 3),
            > 800 => Math.Max(100, configuredThresholdHz * 3 / 4),
            _ => configuredThresholdHz
        };
    }

    /// <summary>Lead time (seconds) from downlink Doppler change rate (Hz/s), for linear look-ahead.</summary>
    internal static double SelectPredictionLeadSeconds(double dopplerRateHzPerSec) =>
        dopplerRateHzPerSec switch
        {
            > 60 => 0.5,
            > 30 => 0.35,
            > 10 => 0.25,
            _ => 0.15
        };

    private static double PredictRangeRateKmPerSec(
        SatelliteTransponderMode mode,
        double rangeRateNowKmPerSec,
        double rangeRateProbeKmPerSec,
        double receiveOffsetKHz,
        double passbandDownlinkAdjustKHz,
        double passbandUplinkAdjustKHz)
    {
        var now = Compute(
            mode,
            rangeRateNowKmPerSec,
            receiveOffsetKHz,
            passbandDownlinkAdjustKHz,
            passbandUplinkAdjustKHz);

        var probe = Compute(
            mode,
            rangeRateProbeKmPerSec,
            receiveOffsetKHz,
            passbandDownlinkAdjustKHz,
            passbandUplinkAdjustKHz);

        var dopplerRateHzPerSec = Math.Abs(now.RadioReceiveKHz - probe.RadioReceiveKHz) * 1000.0 / RangeRateProbeDeltaSec;
        var leadSec = SelectPredictionLeadSeconds(dopplerRateHzPerSec);

        return rangeRateNowKmPerSec
            + (rangeRateProbeKmPerSec - rangeRateNowKmPerSec) / RangeRateProbeDeltaSec * leadSec;
    }

    private static double ComputeShiftKHz(double centerKHz, double rangeRateKmPerSec) =>
        centerKHz * (-rangeRateKmPerSec / SpeedOfLightKmPerSec);
}
