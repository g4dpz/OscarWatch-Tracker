namespace OscarWatch.Core.Models;

public sealed record PassProfilePoint(
    DateTime Utc,
    double AzimuthDeg,
    double ElevationDeg);

public sealed record PassProfile(
    PassInfo Pass,
    IReadOnlyList<PassProfilePoint> Points);
