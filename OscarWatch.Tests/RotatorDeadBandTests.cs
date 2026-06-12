using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Core.Models;
using OscarWatch.Rotator;

namespace OscarWatch.Tests;

public sealed class RotatorDeadBandTests
{
    // ─── Property 1: Invalid threshold values are rejected ───────────────────────

    [Property(DisplayName = "Feature: rotator-dead-band, Property 1: Invalid threshold values are rejected")]
    public bool InvalidThresholdValuesAreRejected(double value)
    {
        if (value >= 0.1 && value <= 10.0 && !double.IsNaN(value) && !double.IsInfinity(value))
            return true; // skip valid values — not under test here

        var settings = new RotatorSettings();
        var originalValue = settings.MovementThresholdDeg;
        settings.MovementThresholdDeg = value;
        return settings.MovementThresholdDeg == originalValue;
    }

    [Fact]
    public void NaN_is_rejected()
    {
        var settings = new RotatorSettings();
        settings.MovementThresholdDeg = double.NaN;
        Assert.Equal(1.0, settings.MovementThresholdDeg);
    }

    [Fact]
    public void PositiveInfinity_is_rejected()
    {
        var settings = new RotatorSettings();
        settings.MovementThresholdDeg = double.PositiveInfinity;
        Assert.Equal(1.0, settings.MovementThresholdDeg);
    }

    [Fact]
    public void NegativeInfinity_is_rejected()
    {
        var settings = new RotatorSettings();
        settings.MovementThresholdDeg = double.NegativeInfinity;
        Assert.Equal(1.0, settings.MovementThresholdDeg);
    }

    [Theory]
    [InlineData(0.09)]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(10.01)]
    [InlineData(100.0)]
    public void OutOfRangeValues_are_rejected(double value)
    {
        var settings = new RotatorSettings();
        settings.MovementThresholdDeg = value;
        Assert.Equal(1.0, settings.MovementThresholdDeg);
    }

    // ─── Property 2: Valid threshold values are accepted ─────────────────────────

    [Property(DisplayName = "Feature: rotator-dead-band, Property 2: Valid threshold values are accepted")]
    public bool ValidThresholdValuesAreAccepted(double raw)
    {
        // Map arbitrary double into valid range [0.1, 10.0]
        var value = 0.1 + Math.Abs(raw % 9.9);
        if (value < 0.1) value = 0.1;
        if (value > 10.0) value = 10.0;
        if (double.IsNaN(value) || double.IsInfinity(value))
            return true; // skip degenerate mapping

        var settings = new RotatorSettings();
        settings.MovementThresholdDeg = value;
        return settings.MovementThresholdDeg == value;
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(1.0)]
    [InlineData(5.0)]
    [InlineData(10.0)]
    public void BoundaryAndMidValues_are_accepted(double value)
    {
        var settings = new RotatorSettings();
        settings.MovementThresholdDeg = value;
        Assert.Equal(value, settings.MovementThresholdDeg);
    }

    [Fact]
    public void DefaultValue_is_one_degree()
    {
        var settings = new RotatorSettings();
        Assert.Equal(1.0, settings.MovementThresholdDeg);
    }

    // ─── Property 3: Dead-band suppression when both axes below threshold ────────

    [Property(DisplayName = "Feature: rotator-dead-band, Property 3: Dead-band suppression when both axes below threshold")]
    public bool DeadBandSuppressionWhenBothAxesBelowThreshold(
        PositiveInt thresholdSeed,
        PositiveInt lastAzSeed,
        PositiveInt lastElSeed)
    {
        // Derive a valid threshold in whole degrees [1, 10]
        var threshold = 1.0 + (thresholdSeed.Get % 10); // [1, 10]
        if (threshold > 10.0) threshold = 10.0;

        // Derive last positions as whole degrees to avoid rounding artefacts
        var lastAz = (double)(lastAzSeed.Get % 180); // [0, 179] to avoid wrap issues
        var lastEl = (double)(5 + lastElSeed.Get % 80); // [5, 84] to stay above track start

        // New position is less than threshold away (half threshold, which is < 1 when threshold=1)
        var delta = threshold * 0.4; // strictly less than threshold
        var newAz = lastAz + delta;
        var newEl = lastEl + delta;

        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            TrackStartElevationDeg = -90,
            MovementThresholdDeg = threshold,
            SmartAzimuth450 = false,
            AzimuthRange = RotatorAzimuthRange.Deg360
        };

        // First command establishes the "last sent" position
        var target1 = MakeTarget(lastAz, lastEl);
        controller.UpdateSynchronously(settings, target1);
        var callsAfterFirst = rotator.SetPositionCallCount;

        // Second command should be suppressed because:
        // The controller stores Math.Round(lastAz) and Math.Round(lastEl),
        // and delta (threshold * 0.4) is always < threshold.
        // Since lastAz is an integer, Round(lastAz) == lastAz, and |newAz - lastAz| = delta < threshold.
        var target2 = MakeTarget(newAz, newEl);
        controller.UpdateSynchronously(settings, target2);

        return rotator.SetPositionCallCount == callsAfterFirst;
    }

    // ─── Property 4: Command sent when either axis meets or exceeds threshold ────

    [Property(DisplayName = "Feature: rotator-dead-band, Property 4: Command sent when either axis meets or exceeds threshold")]
    public bool CommandSentWhenEitherAxisMeetsOrExceedsThreshold(
        PositiveInt thresholdSeed,
        PositiveInt lastAzSeed,
        PositiveInt lastElSeed,
        bool exceedAz)
    {
        // Use whole-degree thresholds to avoid rounding interactions
        var threshold = 1.0 + (thresholdSeed.Get % 10); // [1, 10]
        if (threshold > 10.0) threshold = 10.0;

        // Keep away from boundaries to avoid wrap/clamp issues
        var lastAz = (double)(10 + lastAzSeed.Get % 170); // [10, 179]
        var lastEl = (double)(5 + lastElSeed.Get % 80);   // [5, 84]

        // At least one axis meets or exceeds the threshold
        double newAz, newEl;
        if (exceedAz)
        {
            newAz = lastAz + threshold; // exactly meets threshold in az
            newEl = lastEl;             // no change in el
        }
        else
        {
            newAz = lastAz;             // no change in az
            newEl = lastEl + threshold; // exactly meets threshold in el
        }

        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            TrackStartElevationDeg = -90,
            MovementThresholdDeg = threshold,
            SmartAzimuth450 = false,
            AzimuthRange = RotatorAzimuthRange.Deg360
        };

        // First command establishes baseline
        var target1 = MakeTarget(lastAz, lastEl);
        controller.UpdateSynchronously(settings, target1);
        var callsAfterFirst = rotator.SetPositionCallCount;

        // Second command should be sent
        var target2 = MakeTarget(newAz, newEl);
        controller.UpdateSynchronously(settings, target2);

        return rotator.SetPositionCallCount > callsAfterFirst;
    }

    // ─── Property 5: First command always sent regardless of threshold ────────────

    [Property(DisplayName = "Feature: rotator-dead-band, Property 5: First command always sent regardless of threshold")]
    public bool FirstCommandAlwaysSentRegardlessOfThreshold(
        PositiveInt thresholdSeed,
        PositiveInt azSeed,
        PositiveInt elSeed)
    {
        var threshold = 1.0 + (thresholdSeed.Get % 10); // [1, 10]
        if (threshold > 10.0) threshold = 10.0;

        var az = (double)(azSeed.Get % 360);
        var el = (double)(5 + elSeed.Get % 80); // [5, 84] to stay above track start

        var rotator = new RecordingRotatorDriver();
        var controller = new RotatorController(_ => rotator);
        var settings = new RotatorSettings
        {
            Enabled = true,
            Port = "COM3",
            TrackStartElevationDeg = -90,
            MovementThresholdDeg = threshold,
            SmartAzimuth450 = false,
            AzimuthRange = RotatorAzimuthRange.Deg360
        };

        // Very first command after connection — should always send
        var target = MakeTarget(az, el);
        controller.UpdateSynchronously(settings, target);

        return rotator.SetPositionCallCount == 1;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private static SatelliteTrackState MakeTarget(double azimuthDeg, double elevationDeg) =>
        new()
        {
            Name = "TEST",
            NoradId = "99999",
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(azimuthDeg, elevationDeg, 800, 0)
        };
}
