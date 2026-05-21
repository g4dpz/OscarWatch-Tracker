namespace OscarWatch.Core.Models;

public sealed record GeoCoordinate(double LatitudeDeg, double LongitudeDeg, double AltitudeKm = 0);
