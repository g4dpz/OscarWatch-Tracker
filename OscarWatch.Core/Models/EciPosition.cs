namespace OscarWatch.Core.Models;

/// <summary>Earth-centered inertial position in kilometres (TEME-compatible with OrbitTools).</summary>
public readonly record struct EciPosition(double XKm, double YKm, double ZKm);
