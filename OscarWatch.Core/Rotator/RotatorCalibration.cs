using OscarWatch.Core.Models;

namespace OscarWatch.Core.Rotator;

/// <summary>
/// Applies user-configured az/el calibration offsets before rotator commands are sent.
/// </summary>
public static class RotatorCalibration
{
    public static (double AzimuthDeg, double ElevationDeg) ApplyOffsets(
        double azimuthDeg,
        double elevationDeg,
        RotatorSettings settings)
    {
        var az = Math.Clamp(azimuthDeg + settings.AzimuthOffsetDeg, 0, settings.MaxAzimuthDeg);
        var el = Math.Clamp(elevationDeg + settings.ElevationOffsetDeg, 0, settings.MaxElevationDeg);
        return (az, el);
    }

    public static double? ApplyAzimuthOffset(double? azimuthDeg, RotatorSettings settings) =>
        azimuthDeg is { } az
            ? RotatorAzimuthPlanner.Normalize360(az + settings.AzimuthOffsetDeg)
            : null;
}
