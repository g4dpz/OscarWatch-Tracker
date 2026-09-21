using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;

namespace OscarWatch.Core.Ft4;

/// <summary>Ephemeris Doppler shifts (Hz) for FT4 audio-domain correction.</summary>
public static class Ft4DopplerShift
{
    private const double SpeedOfLightKmPerSec = 299792.458;

    /// <summary>
    /// Instantaneous downlink and uplink Doppler shifts in Hz (same sign convention as
    /// <see cref="DopplerFrequencyCalculator"/>: <c>f · (−rangeRate / c)</c>).
    /// </summary>
    public static (double DownlinkHz, double UplinkHz) ComputeShiftsHz(
        SatelliteTransponderMode mode,
        double rangeRateKmPerSec,
        double receiveOffsetKHz = 0,
        double transmitOffsetKHz = 0)
    {
        var downlinkBase = mode.DownlinkKHz + receiveOffsetKHz;
        var uplinkBase = mode.UplinkKHz + transmitOffsetKHz;
        var dl = ShiftHz(downlinkBase, rangeRateKmPerSec);
        var ul = mode.IsBeaconOnly ? 0 : ShiftHz(uplinkBase, rangeRateKmPerSec);
        return (dl, ul);
    }

    /// <summary>Linear Doppler-shift slope (Hz/s) between two ephemeris samples.</summary>
    public static double SlopeHzPerSec(double shift0Hz, double shift1Hz, double intervalSec)
    {
        if (intervalSec <= 1e-6)
            return 0;
        return (shift1Hz - shift0Hz) / intervalSec;
    }

    private static double ShiftHz(double centerKHz, double rangeRateKmPerSec) =>
        centerKHz * 1000.0 * (-rangeRateKmPerSec / SpeedOfLightKmPerSec);
}
