using OscarWatch.Core.Models;
using OscarWatch.Core.Rotator;

namespace OscarWatch.Tests;

public sealed class RotatorCalibrationTests
{
    [Fact]
    public void ApplyOffsets_adds_configured_values_and_clamps_to_rotator_limits()
    {
        var settings = new RotatorSettings
        {
            AzimuthRange = RotatorAzimuthRange.Deg450,
            ElevationRange = RotatorElevationRange.Deg180,
            AzimuthOffsetDeg = 3.5,
            ElevationOffsetDeg = -2.0
        };

        var (az, el) = RotatorCalibration.ApplyOffsets(180, 10, settings);

        Assert.Equal(183.5, az);
        Assert.Equal(8, el);
    }

    [Fact]
    public void ApplyOffsets_clamps_after_offset()
    {
        var settings = new RotatorSettings
        {
            AzimuthRange = RotatorAzimuthRange.Deg360,
            ElevationRange = RotatorElevationRange.Deg90,
            AzimuthOffsetDeg = 10,
            ElevationOffsetDeg = 5
        };

        var (az, el) = RotatorCalibration.ApplyOffsets(355, 88, settings);

        Assert.Equal(360, az);
        Assert.Equal(90, el);
    }

    [Fact]
    public void ApplyAzimuthOffset_normalizes_result()
    {
        var settings = new RotatorSettings { AzimuthOffsetDeg = 10 };

        Assert.Equal(5, RotatorCalibration.ApplyAzimuthOffset(355, settings));
    }
}
