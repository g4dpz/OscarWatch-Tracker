using OscarWatch.Core.Models;

namespace OscarWatch.Core.Radio;

public static class DopplerFrequencyCalculator
{
    private const double SpeedOfLightKmPerSec = 299792.458;

    public static CorrectedFrequencies Compute(
        SatelliteTransponderMode mode,
        double rangeRateKmPerSec,
        double transmitOffsetKHz,
        double receiveOffsetKHz,
        double manualTransmitAdjustKHz = 0,
        double manualReceiveAdjustKHz = 0)
    {
        var downlink = mode.DownlinkKHz;
        var uplink = mode.UplinkKHz;
        var isBeaconOnly = mode.IsBeaconOnly;

        // NOR: same as QTrig rx_dopplercalc / tx_dopplercalc (rx subtract, tx add on the baseline).
        // REV (inverting V/U): opposite doppler on the radio legs; positive TX offset lowers Radio TX.
        var shiftDown = ComputeShiftKHz(downlink, rangeRateKmPerSec);
        var shiftUp = isBeaconOnly ? 0 : ComputeShiftKHz(uplink, rangeRateKmPerSec);
        var displayShift = shiftDown;

        double radioRx;
        double radioTx;
        if (mode.DopplerCorrection == DopplerCorrection.Reverse)
        {
            radioRx = downlink - shiftDown - receiveOffsetKHz + manualReceiveAdjustKHz;
            radioTx = isBeaconOnly
                ? 0
                : uplink + shiftUp - transmitOffsetKHz + manualTransmitAdjustKHz;
        }
        else
        {
            radioRx = downlink + shiftDown + receiveOffsetKHz + manualReceiveAdjustKHz;
            radioTx = isBeaconOnly
                ? 0
                : uplink - shiftUp + transmitOffsetKHz + manualTransmitAdjustKHz;
        }

        return new CorrectedFrequencies(
            RadioTransmitKHz: radioTx,
            RadioReceiveKHz: radioRx,
            SatelliteTransmitKHz: uplink,
            SatelliteReceiveKHz: downlink,
            DopplerShiftKHz: displayShift,
            IsBeaconOnly: isBeaconOnly);
    }

    private static double ComputeShiftKHz(double centerKHz, double rangeRateKmPerSec) =>
        centerKHz * (-rangeRateKmPerSec / SpeedOfLightKmPerSec);
}
