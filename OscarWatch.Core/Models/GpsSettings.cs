namespace OscarWatch.Core.Models;

public sealed class GpsSettings
{
    public const int DefaultBaudRate = 4800;

    public bool Enabled { get; set; }

    public string Port { get; set; } = "";

    public int BaudRate { get; set; } = DefaultBaudRate;

    /// <summary>Continuously update ground station lat/lon (and optionally altitude) from GPS fix.</summary>
    public bool AutoUpdateStation { get; set; }

    /// <summary>When auto-updating station, apply GGA altitude (meters ASL).</summary>
    public bool UseGpsAltitude { get; set; } = true;

    /// <summary>Use GPS UTC for satellite tracking instead of the system clock.</summary>
    public bool UseGpsTimeForTracking { get; set; }

    /// <summary>Minimum satellites in use before accepting a fix (GGA).</summary>
    public int MinSatellites { get; set; } = 3;

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(Port);
}
