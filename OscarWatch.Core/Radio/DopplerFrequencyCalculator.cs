using OscarWatch.Core.Models;

namespace OscarWatch.Core.Radio;

public static class DopplerFrequencyCalculator
{
    private const double SpeedOfLightKmPerSec = 299792.458;

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
        DopplerStrategy strategy = DopplerStrategy.Full,
        double? transmitRangeRateKmPerSec = null)
    {
        var isBeaconOnly = mode.IsBeaconOnly;
        var txRangeRate = transmitRangeRateKmPerSec ?? rangeRateKmPerSec;

        var downlinkBase = mode.DownlinkKHz + receiveOffsetKHz + passbandDownlinkAdjustKHz;
        var uplinkBase = mode.UplinkKHz + passbandUplinkAdjustKHz;

        var applyRxDoppler = strategy != DopplerStrategy.UplinkOnly;
        var applyTxDoppler = !isBeaconOnly && strategy != DopplerStrategy.DownlinkOnly;

        var shiftDown = applyRxDoppler ? ComputeShiftKHz(downlinkBase, rangeRateKmPerSec) : 0;
        var shiftUp = applyTxDoppler ? ComputeShiftKHz(uplinkBase, txRangeRate) : 0;

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

    private static double ComputeShiftKHz(double centerKHz, double rangeRateKmPerSec) =>
        centerKHz * (-rangeRateKmPerSec / SpeedOfLightKmPerSec);
}
