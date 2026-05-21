using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using Zeptomoby.OrbitTools;
using SatelliteOrbit = Zeptomoby.OrbitTools.Orbit;

namespace OscarWatch.Orbit;

public sealed class BruteForcePassPredictor : IPassPredictor
{
    private static readonly TimeSpan CoarseStep = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RefineTolerance = TimeSpan.FromSeconds(1);

    public Task<IReadOnlyList<PassInfo>> GetPassesAsync(
        SatelliteCatalogEntry satellite,
        GroundStation site,
        DateTime utcStart,
        DateTime utcEnd,
        double minimumElevationDeg,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var orbit = OrbitToolsMapping.CreateOrbit(satellite);
            var groundSite = OrbitToolsMapping.CreateSite(site);

            var passes = new List<PassInfo>();
            var t = utcStart;
            var inPass = false;
            DateTime? aos = null;
            double maxEl = double.MinValue;
            DateTime maxElTime = t;
            double aosAz = 0;
            double losAz = 0;

            double ElAt(DateTime time)
            {
                try
                {
                    var eci = orbit.PositionEci(time);
                    return groundSite.GetLookAngle(eci).ElevationDeg;
                }
                catch
                {
                    return -90;
                }
            }

            while (t <= utcEnd)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var el = ElAt(t);

                if (!inPass && el >= minimumElevationDeg)
                {
                    inPass = true;
                    aos = RefineBoundary(orbit, groundSite, t - CoarseStep, t, minimumElevationDeg, rising: true);
                    aosAz = AzAt(orbit, groundSite, aos.Value);
                    maxEl = el;
                    maxElTime = aos.Value;
                }
                else if (inPass)
                {
                    if (el > maxEl)
                    {
                        maxEl = el;
                        maxElTime = t;
                    }

                    if (el < minimumElevationDeg)
                    {
                        var los = RefineBoundary(orbit, groundSite, t - CoarseStep, t, minimumElevationDeg, rising: false);
                        losAz = AzAt(orbit, groundSite, los);

                        passes.Add(new PassInfo
                        {
                            SatelliteName = satellite.Name,
                            NoradId = satellite.NoradId,
                            AosUtc = aos!.Value,
                            LosUtc = los,
                            MaxElevationDeg = maxEl,
                            MaxElevationUtc = maxElTime,
                            AosAzimuthDeg = aosAz,
                            LosAzimuthDeg = losAz
                        });

                        inPass = false;
                        aos = null;
                        maxEl = double.MinValue;
                    }
                }

                t += CoarseStep;
            }

            return (IReadOnlyList<PassInfo>)passes;
        }, cancellationToken);
    }

    private static DateTime RefineBoundary(
        SatelliteOrbit orbit,
        Site site,
        DateTime before,
        DateTime after,
        double minEl,
        bool rising)
    {
        var lo = before;
        var hi = after;

        while ((hi - lo) > RefineTolerance)
        {
            var mid = lo + (hi - lo) / 2;
            double El(DateTime time)
            {
                try
                {
                    return site.GetLookAngle(orbit.PositionEci(time)).ElevationDeg;
                }
                catch
                {
                    return -90;
                }
            }

            var elMid = El(mid);
            var above = elMid >= minEl;
            if (rising)
            {
                if (above)
                    hi = mid;
                else
                    lo = mid;
            }
            else
            {
                if (above)
                    lo = mid;
                else
                    hi = mid;
            }
        }

        return lo + (hi - lo) / 2;
    }

    private static double AzAt(SatelliteOrbit orbit, Site site, DateTime time)
    {
        try
        {
            return site.GetLookAngle(orbit.PositionEci(time)).AzimuthDeg;
        }
        catch
        {
            return 0;
        }
    }
}
