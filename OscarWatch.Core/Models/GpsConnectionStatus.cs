namespace OscarWatch.Core.Models;

public sealed record GpsConnectionStatus(
    bool IsConnected,
    bool HasFix,
    double? LatitudeDeg,
    double? LongitudeDeg,
    double? AltitudeMeters,
    int? Satellites,
    DateTime? FixUtc,
    string? Detail);
