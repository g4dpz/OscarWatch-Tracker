using OscarWatch.Core.Display;
using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;

namespace OscarWatch.Core.Services;

/// <summary>
/// Samples azimuth/elevation along a pass for the sidebar sky plot path overlay.
/// </summary>
public static class SkyPlotPathBuilder
{
    private static readonly TimeSpan SampleStep = TimeSpan.FromSeconds(15);

    public static IReadOnlyList<SkyPlotPathPoint> Build(
        PassInfo pass,
        IOrbitPropagator propagator,
        GroundStation site,
        double minimumElevationDeg = 0)
    {
        ArgumentNullException.ThrowIfNull(pass);
        ArgumentNullException.ThrowIfNull(propagator);
        ArgumentNullException.ThrowIfNull(site);

        var points = new List<SkyPlotPathPoint>();
        for (var t = pass.AosUtc; t <= pass.LosUtc; t += SampleStep)
            TryAddSample(pass.NoradId, site, propagator, t, points, minimumElevationDeg);

        if (points.Count == 0 || pass.LosUtc > pass.AosUtc)
            TryAddSample(pass.NoradId, site, propagator, pass.LosUtc, points, minimumElevationDeg);

        return points;
    }

    private static void TryAddSample(
        string noradId,
        GroundStation site,
        IOrbitPropagator propagator,
        DateTime utc,
        List<SkyPlotPathPoint> points,
        double minimumElevationDeg)
    {
        try
        {
            var look = propagator.GetLookAngles(noradId, site, utc);
            if (HorizonMaskPolarGeometry.IsObstructed(
                    site.HorizonMask, look.AzimuthDeg, look.ElevationDeg, minimumElevationDeg))
                return;

            var sample = new SkyPlotPathPoint(look.AzimuthDeg, look.ElevationDeg);
            if (points.Count > 0 && points[^1] == sample)
                return;

            points.Add(sample);
        }
        catch
        {
            // Skip failed samples — propagator may throw for certain edge cases.
        }
    }
}
