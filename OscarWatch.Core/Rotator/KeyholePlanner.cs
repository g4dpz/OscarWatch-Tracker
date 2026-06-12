using OscarWatch.Core.Models;

namespace OscarWatch.Core.Rotator;

/// <summary>
/// Pure analysis function that examines a pass profile and determines whether
/// a flipped-start strategy reduces signal loss through the zenith keyhole.
/// </summary>
public static class KeyholePlanner
{
    /// <summary>
    /// Analyses a pass profile and returns a plan recommending normal or flipped start.
    /// </summary>
    public static KeyholePlan Analyse(PassProfile profile, KeyholePlannerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(settings);

        // Non-keyhole passes: MaxElevationDeg below threshold → always Normal, zero signal loss
        if (profile.Pass.MaxElevationDeg < settings.KeyholeThresholdDeg)
        {
            return new KeyholePlan(
                Strategy: KeyholeStrategy.Normal,
                FlippedStartAzimuthDeg: null,
                PrePositionLeadTime: null,
                NormalSignalLossWindow: TimeSpan.Zero,
                FlippedSignalLossWindow: TimeSpan.Zero);
        }

        var normalLoss = ComputeSignalLoss(profile, settings.SlewRateDegPerSec, azimuthOffsetDeg: 0);
        var flippedLoss = ComputeSignalLoss(profile, settings.SlewRateDegPerSec, azimuthOffsetDeg: 180);

        var strategy = flippedLoss < normalLoss
            ? KeyholeStrategy.FlippedStart
            : KeyholeStrategy.Normal;

        double? flippedStartAz = strategy == KeyholeStrategy.FlippedStart
            ? NormalizeAzimuth(profile.Pass.AosAzimuthDeg + 180)
            : null;

        TimeSpan? prePositionLeadTime = strategy == KeyholeStrategy.FlippedStart && flippedStartAz.HasValue
            ? ComputePrePositionLeadTime(settings.ParkAzimuthDeg, flippedStartAz.Value, settings.SlewRateDegPerSec)
            : null;

        return new KeyholePlan(
            Strategy: strategy,
            FlippedStartAzimuthDeg: flippedStartAz,
            PrePositionLeadTime: prePositionLeadTime,
            NormalSignalLossWindow: normalLoss,
            FlippedSignalLossWindow: flippedLoss);
    }

    /// <summary>
    /// Computes the total signal-loss duration for a pass profile given an azimuth offset.
    /// Signal loss occurs when the angular velocity between consecutive profile points
    /// exceeds the rotator's slew rate.
    /// </summary>
    /// <param name="profile">The pass profile with per-second azimuth/elevation points.</param>
    /// <param name="slewRateDegPerSec">Maximum rotator speed in degrees per second.</param>
    /// <param name="azimuthOffsetDeg">Azimuth offset to apply (0° for normal, 180° for flipped).</param>
    /// <returns>Total duration where the rotator cannot keep up with the satellite.</returns>
    internal static TimeSpan ComputeSignalLoss(PassProfile profile, double slewRateDegPerSec, double azimuthOffsetDeg)
    {
        if (profile.Points.Count < 2)
            return TimeSpan.Zero;

        var totalLossSeconds = 0.0;

        for (var i = 0; i < profile.Points.Count - 1; i++)
        {
            var current = profile.Points[i];
            var next = profile.Points[i + 1];

            var currentAz = NormalizeAzimuth(current.AzimuthDeg + azimuthOffsetDeg);
            var nextAz = NormalizeAzimuth(next.AzimuthDeg + azimuthOffsetDeg);

            var deltaAz = ShortestAngularDistance(currentAz, nextAz);
            var deltaTimeSeconds = (next.Utc - current.Utc).TotalSeconds;

            if (deltaTimeSeconds <= 0)
                continue;

            var angularVelocity = deltaAz / deltaTimeSeconds;

            if (angularVelocity > slewRateDegPerSec)
                totalLossSeconds += deltaTimeSeconds;
        }

        return TimeSpan.FromSeconds(totalLossSeconds);
    }

    /// <summary>
    /// Computes the pre-position lead time: how long before AOS the rotator must
    /// begin slewing to reach the flipped start azimuth, plus a 5-second settling margin.
    /// </summary>
    internal static TimeSpan ComputePrePositionLeadTime(
        double parkAzimuthDeg,
        double flippedStartAzimuthDeg,
        double slewRateDegPerSec)
    {
        var distance = ShortestAngularDistance(parkAzimuthDeg, flippedStartAzimuthDeg);
        var slewTimeSeconds = distance / slewRateDegPerSec;
        return TimeSpan.FromSeconds(slewTimeSeconds + 5.0);
    }

    /// <summary>
    /// Returns the shortest angular distance between two compass bearings.
    /// Result is always in [0, 180].
    /// </summary>
    /// <remarks>
    /// Handles the 359°→1° wrap correctly.
    /// Formula: min(|a-b|, 360 - |a-b|) where both values are normalised to [0, 360).
    /// </remarks>
    internal static double ShortestAngularDistance(double aDeg, double bDeg)
    {
        var a = NormalizeAzimuth(aDeg);
        var b = NormalizeAzimuth(bDeg);
        var diff = Math.Abs(a - b);
        return Math.Min(diff, 360.0 - diff);
    }

    /// <summary>
    /// Normalises an azimuth value to [0, 360).
    /// </summary>
    internal static double NormalizeAzimuth(double deg)
    {
        deg %= 360.0;
        if (deg < 0)
            deg += 360.0;
        return deg;
    }
}
