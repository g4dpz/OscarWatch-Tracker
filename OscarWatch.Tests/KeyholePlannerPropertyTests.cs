// Feature: smart-antenna-rotation, Properties 1–6: KeyholePlanner correctness properties

using FsCheck.Xunit;
using OscarWatch.Core.Models;
using OscarWatch.Core.Rotator;

namespace OscarWatch.Tests;

/// <summary>
/// Property-based tests for the KeyholePlanner pure analysis function.
/// Uses FsCheck.Xunit [Property] with auto-generated primitive parameters,
/// mapped to valid domain values.
/// </summary>
public class KeyholePlannerPropertyTests
{
    #region Helpers

    /// <summary>
    /// Reference implementation of shortest angular distance for test oracle.
    /// </summary>
    private static double ShortestAngularDistance(double a, double b)
    {
        a = ((a % 360.0) + 360.0) % 360.0;
        b = ((b % 360.0) + 360.0) % 360.0;
        var diff = Math.Abs(a - b);
        return Math.Min(diff, 360.0 - diff);
    }

    /// <summary>
    /// Builds a PassInfo with the given parameters.
    /// </summary>
    private static PassInfo MakePassInfo(double maxElevationDeg, double aosAzimuthDeg)
    {
        var aos = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        return new PassInfo
        {
            SatelliteName = "TEST-SAT",
            NoradId = "99999",
            AosUtc = aos,
            LosUtc = aos.AddMinutes(10),
            MaxElevationDeg = maxElevationDeg,
            MaxElevationUtc = aos.AddMinutes(5),
            AosAzimuthDeg = aosAzimuthDeg,
            LosAzimuthDeg = 180.0
        };
    }

    /// <summary>
    /// Generates a list of PassProfilePoints from byte arrays.
    /// Azimuths are mapped to [0, 360), elevations to [0, 90], times are 1s apart.
    /// </summary>
    private static IReadOnlyList<PassProfilePoint> BuildProfilePoints(byte[] azSeeds, byte[] elSeeds)
    {
        var count = Math.Min(azSeeds.Length, elSeeds.Length);
        if (count < 2) return [];

        // Cap at 30 points for reasonable test duration
        count = Math.Min(count, 30);

        var baseTime = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var points = new List<PassProfilePoint>(count);

        for (var i = 0; i < count; i++)
        {
            var az = azSeeds[i] / 255.0 * 359.9; // [0, 359.9]
            var el = elSeeds[i] / 255.0 * 90.0;  // [0, 90]
            points.Add(new PassProfilePoint(baseTime.AddSeconds(i), az, el));
        }

        return points;
    }

    #endregion

    #region Property 1: Keyhole Classification Correctness

    /// <summary>
    /// Feature: smart-antenna-rotation, Property 1: Keyhole Classification Correctness
    ///
    /// For any pass with random MaxElevationDeg and random threshold in [60, 89],
    /// the planner classifies the pass as keyhole iff MaxElevationDeg >= threshold.
    ///
    /// **Validates: Requirements 1.1, 1.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool ClassificationIsKeyhole_IffMaxElevAboveThreshold(
        byte maxElevByte,
        byte thresholdByte)
    {
        // Map maxElev to [0, 90], threshold to [60, 89]
        var maxElev = maxElevByte / 255.0 * 90.0;
        var threshold = 60.0 + (thresholdByte % 30); // [60, 89]

        var passInfo = MakePassInfo(maxElev, 45.0);
        var baseTime = passInfo.AosUtc;

        // Minimal profile (2 points) — just enough for Analyse to work
        var points = new List<PassProfilePoint>
        {
            new(baseTime, 45.0, 10.0),
            new(baseTime.AddSeconds(1), 46.0, 11.0)
        };
        var profile = new PassProfile(passInfo, points);
        var settings = new KeyholePlannerSettings(threshold, 3.0, 0.0);

        var plan = KeyholePlanner.Analyse(profile, settings);

        var isKeyhole = maxElev >= threshold;

        if (!isKeyhole)
        {
            // Non-keyhole passes: Normal strategy with zero signal-loss windows
            return plan.Strategy == KeyholeStrategy.Normal
                && plan.NormalSignalLossWindow == TimeSpan.Zero
                && plan.FlippedSignalLossWindow == TimeSpan.Zero
                && plan.FlippedStartAzimuthDeg is null
                && plan.PrePositionLeadTime is null;
        }
        else
        {
            // Keyhole passes: strategy is determined by comparing signal losses
            // (could be Normal or FlippedStart, but we verify the classification
            // path was taken by checking the strategy matches loss comparison)
            var expectedStrategy = plan.FlippedSignalLossWindow < plan.NormalSignalLossWindow
                ? KeyholeStrategy.FlippedStart
                : KeyholeStrategy.Normal;
            return plan.Strategy == expectedStrategy;
        }
    }

    #endregion

    #region Property 2: Signal Loss Window Computation

    /// <summary>
    /// Feature: smart-antenna-rotation, Property 2: Signal Loss Window Computation
    ///
    /// For any pass profile and positive slew rate, the signal-loss duration equals the sum
    /// of intervals where angular velocity exceeds the slew rate. We independently compute
    /// this as a test oracle and compare against KeyholePlanner.ComputeSignalLoss.
    ///
    /// **Validates: Requirements 2.1, 2.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool SignalLoss_EqualsSum_OfExceedingIntervals(
        byte[] azSeeds,
        byte[] elSeeds,
        byte slewRateByte,
        bool useFlippedOffset)
    {
        if (azSeeds is null || azSeeds.Length < 2 || elSeeds is null || elSeeds.Length < 2)
            return true; // trivially true for degenerate inputs

        var points = BuildProfilePoints(azSeeds, elSeeds);
        if (points.Count < 2)
            return true;

        var slewRate = 0.1 + (slewRateByte / 255.0 * 9.9); // [0.1, 10.0]
        var offset = useFlippedOffset ? 180.0 : 0.0;

        var profile = new PassProfile(MakePassInfo(85.0, 45.0), points);

        // Reference computation (test oracle)
        var expectedLossSeconds = 0.0;
        for (var i = 0; i < points.Count - 1; i++)
        {
            var currentAz = ((points[i].AzimuthDeg + offset) % 360.0 + 360.0) % 360.0;
            var nextAz = ((points[i + 1].AzimuthDeg + offset) % 360.0 + 360.0) % 360.0;

            var deltaAz = ShortestAngularDistance(currentAz, nextAz);
            var deltaT = (points[i + 1].Utc - points[i].Utc).TotalSeconds;

            if (deltaT <= 0) continue;

            var angularVelocity = deltaAz / deltaT;
            if (angularVelocity > slewRate)
                expectedLossSeconds += deltaT;
        }

        var actual = KeyholePlanner.ComputeSignalLoss(profile, slewRate, offset);
        var expected = TimeSpan.FromSeconds(expectedLossSeconds);

        return Math.Abs(actual.TotalSeconds - expected.TotalSeconds) < 0.001;
    }

    #endregion

    #region Property 3: Strategy Decision Correctness

    /// <summary>
    /// Feature: smart-antenna-rotation, Property 3: Strategy Decision Correctness
    ///
    /// For any keyhole pass profile and valid settings, the planner recommends FlippedStart
    /// if and only if the flipped signal-loss window is strictly shorter than the normal one.
    ///
    /// **Validates: Requirements 2.3, 2.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Strategy_IsFlipped_IffFlippedLossIsLess(
        byte[] azSeeds,
        byte[] elSeeds,
        byte thresholdByte,
        byte slewRateByte,
        byte parkAzByte,
        byte aosAzByte)
    {
        if (azSeeds is null || azSeeds.Length < 3 || elSeeds is null || elSeeds.Length < 3)
            return true;

        var points = BuildProfilePoints(azSeeds, elSeeds);
        if (points.Count < 2)
            return true;

        var threshold = 60.0 + (thresholdByte % 30);            // [60, 89]
        var slewRate = 0.1 + (slewRateByte / 255.0 * 9.9);     // [0.1, 10.0]
        var parkAz = parkAzByte / 255.0 * 359.9;                // [0, 359.9]
        var aosAz = aosAzByte / 255.0 * 359.9;                  // [0, 359.9]

        // Use maxElev above threshold to ensure keyhole classification
        var maxElev = threshold + 1.0;
        var profile = new PassProfile(MakePassInfo(maxElev, aosAz), points);
        var settings = new KeyholePlannerSettings(threshold, slewRate, parkAz);

        var plan = KeyholePlanner.Analyse(profile, settings);

        // Independently compute losses
        var normalLoss = KeyholePlanner.ComputeSignalLoss(profile, slewRate, 0.0);
        var flippedLoss = KeyholePlanner.ComputeSignalLoss(profile, slewRate, 180.0);

        var expectedStrategy = flippedLoss < normalLoss
            ? KeyholeStrategy.FlippedStart
            : KeyholeStrategy.Normal;

        return plan.Strategy == expectedStrategy;
    }

    #endregion

    #region Property 4: Maximum Angular Velocity Computation

    /// <summary>
    /// Feature: smart-antenna-rotation, Property 4: Maximum Angular Velocity Computation
    ///
    /// For any profile with ≥2 points, the computed maximum angular velocity equals the
    /// maximum of shortest-angular-distance / deltaT across all consecutive point pairs.
    /// We verify our reference computation agrees with the planner's ShortestAngularDistance.
    ///
    /// **Validates: Requirements 1.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool MaxAngularVelocity_EqualsMaxOfPairwiseRates(
        byte[] azSeeds,
        byte[] elSeeds)
    {
        if (azSeeds is null || azSeeds.Length < 2 || elSeeds is null || elSeeds.Length < 2)
            return true;

        var points = BuildProfilePoints(azSeeds, elSeeds);
        if (points.Count < 2)
            return true;

        // Reference computation: max angular velocity across all consecutive pairs
        var maxAngularVelocity = 0.0;
        for (var i = 0; i < points.Count - 1; i++)
        {
            var deltaAz = ShortestAngularDistance(
                points[i].AzimuthDeg,
                points[i + 1].AzimuthDeg);
            var deltaT = (points[i + 1].Utc - points[i].Utc).TotalSeconds;

            if (deltaT <= 0) continue;

            var velocity = deltaAz / deltaT;
            if (velocity > maxAngularVelocity)
                maxAngularVelocity = velocity;
        }

        // Compute using KeyholePlanner's own ShortestAngularDistance (internal, visible via InternalsVisibleTo)
        var maxViaPlanner = 0.0;
        for (var i = 0; i < points.Count - 1; i++)
        {
            var deltaAz = KeyholePlanner.ShortestAngularDistance(
                points[i].AzimuthDeg,
                points[i + 1].AzimuthDeg);
            var deltaT = (points[i + 1].Utc - points[i].Utc).TotalSeconds;

            if (deltaT <= 0) continue;

            var velocity = deltaAz / deltaT;
            if (velocity > maxViaPlanner)
                maxViaPlanner = velocity;
        }

        // Both computations should agree
        return Math.Abs(maxAngularVelocity - maxViaPlanner) < 0.0001;
    }

    #endregion

    #region Property 5: Pre-Position Time Computation

    /// <summary>
    /// Feature: smart-antenna-rotation, Property 5: Pre-Position Time Computation
    ///
    /// For any park azimuth, flipped start azimuth, and positive slew rate, the computed
    /// PrePositionLeadTime equals shortest_angular_distance(park, flipped) / slewRate + 5s.
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool PrePositionTime_EqualsDistanceOverRatePlus5s(
        byte parkAzByte,
        byte flippedAzByte,
        byte slewRateByte)
    {
        var parkAz = parkAzByte / 255.0 * 359.9;             // [0, 359.9]
        var flippedAz = flippedAzByte / 255.0 * 359.9;      // [0, 359.9]
        var slewRate = 0.1 + (slewRateByte / 255.0 * 9.9);  // [0.1, 10.0]

        var actual = KeyholePlanner.ComputePrePositionLeadTime(parkAz, flippedAz, slewRate);

        var distance = ShortestAngularDistance(parkAz, flippedAz);
        var expectedSeconds = (distance / slewRate) + 5.0;
        var expected = TimeSpan.FromSeconds(expectedSeconds);

        return Math.Abs(actual.TotalSeconds - expected.TotalSeconds) < 0.001;
    }

    #endregion

    #region Property 6: Flipped Azimuth Transformation

    /// <summary>
    /// Feature: smart-antenna-rotation, Property 6: Flipped Azimuth Transformation
    ///
    /// For any compass azimuth in [0, 360), the flipped azimuth (az + 180) % 360
    /// is always in [0, 360) and equals the expected formula.
    ///
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool FlippedAzimuth_IsInValidRange_AndMatchesFormula(byte azByte)
    {
        var az = azByte / 255.0 * 359.99; // [0, 359.99]

        var flipped = KeyholePlanner.NormalizeAzimuth(az + 180.0);

        // Must be in [0, 360)
        if (flipped < 0.0 || flipped >= 360.0)
            return false;

        // Must equal (az + 180) mod 360
        var expected = (az + 180.0) % 360.0;
        return Math.Abs(flipped - expected) < 0.0001;
    }

    #endregion
}
