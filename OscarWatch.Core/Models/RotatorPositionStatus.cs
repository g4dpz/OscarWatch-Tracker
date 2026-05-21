namespace OscarWatch.Core.Models;

public sealed record RotatorPositionStatus(bool IsConnected, int? AzimuthDeg, int? ElevationDeg);
