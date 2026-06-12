// Feature: smart-antenna-rotation, Properties 8–9 + Unit Tests: RotatorSettings keyhole validation

using FsCheck.Xunit;
using OscarWatch.Core.Models;
using Xunit;

namespace OscarWatch.Tests;

/// <summary>
/// Property-based and unit tests for RotatorSettings keyhole avoidance properties:
/// - SlewRateDegPerSec validation (rejects ≤ 0)
/// - KeyholeThresholdDeg validation (rejects outside [60, 89])
/// - Default values for keyhole avoidance settings
/// </summary>
public class RotatorSettingsKeyholeTests
{
    #region Property 8: Slew Rate Validation

    /// <summary>
    /// Feature: smart-antenna-rotation, Property 8: Slew Rate Validation
    ///
    /// For any value ≤ 0, setting SlewRateDegPerSec is rejected and the previous value
    /// (the default 3.0) is retained.
    ///
    /// **Validates: Requirements 7.3**
    /// </summary>
    [Property(MaxTest = 200)]
    public bool SlewRate_RejectsNonPositive_RetainsPrevious(int rawValue)
    {
        // Map rawValue to a non-positive double: use negative or zero
        // Generate values across the non-positive range including zero and very negative
        var invalidRate = rawValue <= 0 ? (double)rawValue : -(double)rawValue;
        // Ensure we always have a non-positive value
        if (invalidRate > 0) invalidRate = 0.0;

        var settings = new RotatorSettings();
        var defaultValue = settings.SlewRateDegPerSec; // should be 3.0

        settings.SlewRateDegPerSec = invalidRate;

        // Value must remain at default (3.0) after rejected assignment
        return settings.SlewRateDegPerSec == defaultValue;
    }

    /// <summary>
    /// Feature: smart-antenna-rotation, Property 8: Slew Rate Validation (positive case)
    ///
    /// For any positive value, setting SlewRateDegPerSec is accepted.
    ///
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Property(MaxTest = 200)]
    public bool SlewRate_AcceptsPositive(byte rawByte)
    {
        // Map to a positive value: (0, 100]
        var validRate = 0.01 + (rawByte / 255.0 * 99.99); // [0.01, 100.0]

        var settings = new RotatorSettings();
        settings.SlewRateDegPerSec = validRate;

        return Math.Abs(settings.SlewRateDegPerSec - validRate) < 0.0001;
    }

    #endregion

    #region Property 9: Keyhole Threshold Validation

    /// <summary>
    /// Feature: smart-antenna-rotation, Property 9: Keyhole Threshold Validation
    ///
    /// For any value outside [60, 89], setting KeyholeThresholdDeg is rejected and
    /// the previous value (the default 80.0) is retained.
    ///
    /// **Validates: Requirements 8.3**
    /// </summary>
    [Property(MaxTest = 200)]
    public bool KeyholeThreshold_RejectsOutsideRange_RetainsPrevious(double rawValue)
    {
        // Only test with values actually outside [60, 89]
        if (rawValue >= 60.0 && rawValue <= 89.0)
            return true; // trivially true — skip in-range values

        // Also skip NaN/Infinity which have undefined comparison behaviour
        if (double.IsNaN(rawValue) || double.IsInfinity(rawValue))
            return true;

        var settings = new RotatorSettings();
        var defaultValue = settings.KeyholeThresholdDeg; // should be 80.0

        settings.KeyholeThresholdDeg = rawValue;

        // Value must remain at default (80.0) after rejected assignment
        return settings.KeyholeThresholdDeg == defaultValue;
    }

    /// <summary>
    /// Feature: smart-antenna-rotation, Property 9: Keyhole Threshold Validation (valid case)
    ///
    /// For any value within [60, 89], setting KeyholeThresholdDeg is accepted.
    ///
    /// **Validates: Requirements 8.2**
    /// </summary>
    [Property(MaxTest = 200)]
    public bool KeyholeThreshold_AcceptsWithinRange(byte rawByte)
    {
        // Map byte to [60, 89]
        var validThreshold = 60.0 + (rawByte / 255.0 * 29.0); // [60.0, 89.0]

        var settings = new RotatorSettings();
        settings.KeyholeThresholdDeg = validThreshold;

        return Math.Abs(settings.KeyholeThresholdDeg - validThreshold) < 0.0001;
    }

    #endregion

    #region Unit Tests: Default Settings Values

    /// <summary>
    /// Feature: smart-antenna-rotation, Unit Test: Default settings values
    ///
    /// **Validates: Requirements 6.1**
    /// </summary>
    [Fact]
    public void KeyholeAvoidanceEnabled_DefaultsToFalse()
    {
        var settings = new RotatorSettings();

        Assert.False(settings.KeyholeAvoidanceEnabled);
    }

    /// <summary>
    /// Feature: smart-antenna-rotation, Unit Test: Default settings values
    ///
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Fact]
    public void SlewRateDegPerSec_DefaultsTo3()
    {
        var settings = new RotatorSettings();

        Assert.Equal(3.0, settings.SlewRateDegPerSec);
    }

    /// <summary>
    /// Feature: smart-antenna-rotation, Unit Test: Default settings values
    ///
    /// **Validates: Requirements 8.2**
    /// </summary>
    [Fact]
    public void KeyholeThresholdDeg_DefaultsTo80()
    {
        var settings = new RotatorSettings();

        Assert.Equal(80.0, settings.KeyholeThresholdDeg);
    }

    #endregion
}
