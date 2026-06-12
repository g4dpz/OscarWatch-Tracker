namespace OscarWatch.Core.Models;

public enum RotatorAzimuthRange
{
    Deg360 = 360,
    Deg450 = 450
}

public enum RotatorElevationRange
{
    Deg90 = 90,
    Deg180 = 180
}

public sealed class RotatorSettings
{
    public bool Enabled { get; set; }
    public RotatorType Type { get; set; } = RotatorType.YaesuGs232;
    public string Port { get; set; } = "";
    public int BaudRate { get; set; } = 4800;
    public RotatorAzimuthRange AzimuthRange { get; set; } = RotatorAzimuthRange.Deg450;
    public RotatorElevationRange ElevationRange { get; set; } = RotatorElevationRange.Deg180;
    /// <summary>Start slewing when satellite elevation reaches this value while approaching.</summary>
    public double TrackStartElevationDeg { get; set; } = -3;
    public double ParkAzimuthDeg { get; set; }
    public double ParkElevationDeg { get; set; }

    /// <summary>Added to commanded azimuth for tracking, park, and manual moves.</summary>
    public double AzimuthOffsetDeg { get; set; }

    /// <summary>Added to commanded elevation for tracking, park, and manual moves.</summary>
    public double ElevationOffsetDeg { get; set; }

    /// <summary>Use 361–450° commands for shortest path when <see cref="AzimuthRange"/> is 450°.</summary>
    public bool SmartAzimuth450 { get; set; } = true;

    /// <summary>
    /// Minimum angular change (degrees) required before a new position command is sent.
    /// Valid range: [0.1, 10.0]. Default: 1.0°.
    /// </summary>
    private double _movementThresholdDeg = 1.0;
    public double MovementThresholdDeg
    {
        get => _movementThresholdDeg;
        set
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.1 || value > 10.0) return;
            _movementThresholdDeg = value;
        }
    }

    public double MaxAzimuthDeg => (double)AzimuthRange;
    public double MaxElevationDeg => (double)ElevationRange;
}
