using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;

namespace OscarWatch.Core.Services;

/// <summary>
/// Builds an elevation profile (time/elevation sample array) for a single satellite pass
/// by sampling the orbit propagator at regular intervals between AOS and LOS.
/// </summary>
public static class ElevationProfileBuilder
{
    /// <summary>
    /// Samples elevation at <paramref name="sampleInterval"/> intervals between AOS and LOS.
    /// Times are expressed as minutes from <paramref name="referenceUtc"/>.
    /// </summary>
    /// <param name="pass">The pass to profile.</param>
    /// <param name="propagator">Orbit propagator providing look-angle computation.</param>
    /// <param name="site">Ground station location.</param>
    /// <param name="sampleInterval">Interval between samples (typically 30 seconds).</param>
    /// <param name="referenceUtc">Reference time for MinutesFromNow calculation (typically DateTime.UtcNow).</param>
    /// <returns>An ordered list of elevation samples from AOS to LOS.</returns>
    public static IReadOnlyList<ElevationSample> Build(
        PassInfo pass,
        IOrbitPropagator propagator,
        GroundStation site,
        TimeSpan sampleInterval,
        DateTime referenceUtc)
    {
        if (pass is null) throw new ArgumentNullException(nameof(pass));
        if (propagator is null) throw new ArgumentNullException(nameof(propagator));
        if (site is null) throw new ArgumentNullException(nameof(site));
        if (sampleInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(sampleInterval));

        var samples = new List<ElevationSample>();

        // Include AOS endpoint at 0°
        samples.Add(new ElevationSample(
            (pass.AosUtc - referenceUtc).TotalMinutes,
            0.0));

        var t = pass.AosUtc + sampleInterval;
        while (t < pass.LosUtc)
        {
            try
            {
                var look = propagator.GetLookAngles(pass.NoradId, site, t);
                var elev = Math.Max(0, look.ElevationDeg);
                samples.Add(new ElevationSample(
                    (t - referenceUtc).TotalMinutes,
                    elev));
            }
            catch
            {
                // Skip failed samples — propagator may throw for certain edge cases
            }

            t += sampleInterval;
        }

        // Include LOS endpoint at 0°
        samples.Add(new ElevationSample(
            (pass.LosUtc - referenceUtc).TotalMinutes,
            0.0));

        return samples;
    }
}
