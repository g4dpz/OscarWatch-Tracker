using OscarWatch.Core.Models;

namespace OscarWatch.Core.Radio;

public static class DopplerFrequencyCalculator
{
    private const double SpeedOfLightKmPerSec = 299792.458;

    public static CorrectedFrequencies Compute(
        SatelliteTransponderMode mode,
        double rangeRateKmPerSec,
        double transmitOffsetKHz,
        double receiveOffsetKHz)
    {
        var downlink = mode.DownlinkKHz;
        var uplink = mode.UplinkKHz;
        var isBeaconOnly = mode.IsBeaconOnly;

        var shiftDown = ComputeShiftKHz(downlink, rangeRateKmPerSec);
        var shiftUp = isBeaconOnly ? 0 : ComputeShiftKHz(uplink, rangeRateKmPerSec);
        var displayShift = shiftDown;

        double radioRx;
        double radioTx;
        if (mode.DopplerCorrection == DopplerCorrection.Reverse)
        {
            radioRx = downlink - shiftDown + receiveOffsetKHz;
            radioTx = isBeaconOnly ? 0 : uplink + shiftUp + transmitOffsetKHz;
        }
        else
        {
            radioRx = downlink + shiftDown + receiveOffsetKHz;
            radioTx = isBeaconOnly ? 0 : uplink - shiftUp + transmitOffsetKHz;
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
