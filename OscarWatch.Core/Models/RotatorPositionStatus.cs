namespace OscarWatch.Core.Models;

public sealed record RotatorPositionStatus(
    bool IsConnected,
    int? AzimuthDeg,
    int? ElevationDeg,
    int? CommandedAzimuthDeg = null,
    int? CompassAzimuthDeg = null,
    bool IsParked = false);
