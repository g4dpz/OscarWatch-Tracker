namespace OscarWatch.Core.Models;

public sealed record KeyholePlannerSettings(
    double KeyholeThresholdDeg,
    double SlewRateDegPerSec,
    double ParkAzimuthDeg);
