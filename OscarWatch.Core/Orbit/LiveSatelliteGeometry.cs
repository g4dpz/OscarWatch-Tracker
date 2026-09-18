using OscarWatch.Core.Models;

namespace OscarWatch.Core.Orbit;

/// <summary>
/// Look angles (optional), subpoint, and ECI from a single propagation epoch.
/// When look angles fail, <see cref="LookAngles"/> is null and <see cref="LookAnglesError"/> may carry the cause.
/// </summary>
public readonly record struct LiveSatelliteGeometry(
    LookAngles? LookAngles,
    GeoCoordinate Subpoint,
    EciPosition Eci,
    Exception? LookAnglesError = null);
