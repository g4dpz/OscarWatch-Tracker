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

    /// <summary>When true, the keyhole avoidance system analyses high-elevation passes and may pre-position the rotator in a flipped orientation.</summary>
    public bool KeyholeAvoidanceEnabled { get; set; } = false;

    /// <summary>Maximum rotator slew rate in degrees per second, used for keyhole signal-loss computation.</summary>
    public double SlewRateDegPerSec { get; set; } = 3.0;

    /// <summary>Minimum max-elevation (degrees) for a pass to be classified as entering the keyhole zone. Valid range: [60, 89].</summary>
    public double KeyholeThresholdDeg { get; set; } = 80.0;

    public double MaxAzimuthDeg => (double)AzimuthRange;
    public double MaxElevationDeg => (double)ElevationRange;
}
