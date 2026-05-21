namespace OscarWatch.Core.Models;

public sealed record LookAngles(
    double AzimuthDeg,
    double ElevationDeg,
    double RangeKm,
    double RangeRateKmPerSec = 0);
