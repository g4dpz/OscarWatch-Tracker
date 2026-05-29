using Zeptomoby.OrbitTools;

namespace OscarWatch.Orbit;

internal static class RangeRateCalculator
{
    /// <summary>
    /// Instantaneous range rate (km/s, positive when range increasing) from ECI positions and velocities.
    /// </summary>
    public static double ComputeKmPerSec(EciTime satellite, EciTime observer)
    {
        var dx = satellite.Position.X - observer.Position.X;
        var dy = satellite.Position.Y - observer.Position.Y;
        var dz = satellite.Position.Z - observer.Position.Z;
        var range = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (range <= 0)
            return 0;

        var vx = satellite.Velocity.X - observer.Velocity.X;
        var vy = satellite.Velocity.Y - observer.Velocity.Y;
        var vz = satellite.Velocity.Z - observer.Velocity.Z;
        return (dx * vx + dy * vy + dz * vz) / range;
    }
}
