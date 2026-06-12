using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;

namespace OscarWatch.Core.Rotator;

/// <summary>
/// Builds a <see cref="PassProfile"/> by calling <see cref="IOrbitPropagator.GetLookAngles"/>
/// at 1-second intervals from AOS to LOS.
/// </summary>
public static class PassProfileBuilder
{
    /// <summary>
    /// Builds a pass profile for the given pass by sampling look angles every second.
    /// </summary>
    /// <param name="pass">The pass to profile (AOS to LOS).</param>
    /// <param name="noradId">NORAD catalogue ID of the satellite.</param>
    /// <param name="site">Observer ground station.</param>
    /// <param name="propagator">Orbit propagator providing look angles.</param>
    /// <returns>
    /// A <see cref="PassProfile"/> containing per-second azimuth/elevation points,
    /// or <c>null</c> if more than 50% of expected points failed to compute.
    /// </returns>
    public static PassProfile? Build(
        PassInfo pass,
        string noradId,
        GroundStation site,
        IOrbitPropagator propagator)
    {
        ArgumentNullException.ThrowIfNull(pass);
        ArgumentNullException.ThrowIfNull(noradId);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(propagator);

        var totalSeconds = (int)Math.Ceiling((pass.LosUtc - pass.AosUtc).TotalSeconds);
        var expectedCount = totalSeconds + 1; // inclusive of both AOS and LOS seconds
        var points = new List<PassProfilePoint>(expectedCount);
        var failedCount = 0;

        for (var i = 0; i < expectedCount; i++)
        {
            var utc = pass.AosUtc.AddSeconds(i);

            try
            {
                var lookAngles = propagator.GetLookAngles(noradId, site, utc);
                points.Add(new PassProfilePoint(utc, lookAngles.AzimuthDeg, lookAngles.ElevationDeg));
            }
            catch
            {
                failedCount++;
            }
        }

        // If more than 50% of points failed, return null (caller falls back to normal tracking)
        if (expectedCount > 0 && failedCount > expectedCount / 2)
            return null;

        return new PassProfile(pass, points);
    }
}
