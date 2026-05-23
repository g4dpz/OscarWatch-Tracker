using OscarWatch.Core.Models;

namespace OscarWatch.Core.Radio;

public static class DopplerFrequencyCalculator
{
    private const double SpeedOfLightKmPerSec = 299792.458;

    /// <summary>
    /// Applies the same doppler formula to downlink and uplink for NOR and REV.
    /// REV passband coupling is handled outside this type (Main dial mutates passband baselines).
    /// Receive offset is applied to the downlink nominal before doppler is computed.
    /// </summary>
    public static CorrectedFrequencies Compute(
        SatelliteTransponderMode mode,
        double rangeRateKmPerSec,
        double receiveOffsetKHz,
        double passbandDownlinkAdjustKHz = 0,
        double passbandUplinkAdjustKHz = 0)
    {
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
            SatelliteTransmitKHz: mode.UplinkKHz,
            SatelliteReceiveKHz: mode.DownlinkKHz,
            DopplerShiftKHz: shiftDown,
            IsBeaconOnly: isBeaconOnly);
    }

    private static double ComputeShiftKHz(double centerKHz, double rangeRateKmPerSec) =>
        centerKHz * (-rangeRateKmPerSec / SpeedOfLightKmPerSec);
}
