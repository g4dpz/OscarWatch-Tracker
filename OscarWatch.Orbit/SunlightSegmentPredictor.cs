using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using SatelliteOrbit = Zeptomoby.OrbitTools.Orbit;

namespace OscarWatch.Orbit;

public sealed class SunlightSegmentPredictor : IIlluminationPredictor
{
    private static readonly TimeSpan CoarseStep = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RefineTolerance = TimeSpan.FromSeconds(1);

    public Task<IReadOnlyList<IlluminationSegment>> PredictAsync(
        SatelliteCatalogEntry satellite,
        DateTime utcStart,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => PredictCore(satellite, utcStart, duration, cancellationToken),
            cancellationToken);
    }

    private static IReadOnlyList<IlluminationSegment> PredictCore(
        SatelliteCatalogEntry satellite,
        DateTime utcStart,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        var orbit = OrbitToolsMapping.CreateOrbit(satellite);
        var utcEnd = utcStart + duration;
        var segments = new List<IlluminationSegment>();

        bool IsSunlit(DateTime time)
        {
            try
            {
                var sun = SunPositionCalculator.GetPosition(time);
                var sat = OrbitToolsMapping.ToEciPosition(orbit.PositionEci(time));
                return SatelliteIllumination.IsSunlit(sat, sun);
            }
            catch
            {
                return true;
            }
        }

        var t = utcStart;
        var currentSunlit = IsSunlit(t);
        var segmentStart = t;

        while (t <= utcEnd)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sunlit = IsSunlit(t);
            if (sunlit != currentSunlit)
            {
                var boundary = RefineTransition(orbit, t - CoarseStep, t, currentSunlit);
                segments.Add(new IlluminationSegment
                {
                    StartUtc = segmentStart,
                    EndUtc = boundary,
                    IsSunlit = currentSunlit
                });
                segmentStart = boundary;
                currentSunlit = sunlit;
            }

            t += CoarseStep;
        }

        if (segmentStart < utcEnd)
        {
            segments.Add(new IlluminationSegment
            {
                StartUtc = segmentStart,
                EndUtc = utcEnd,
                IsSunlit = currentSunlit
            });
        }

        return segments;
    }

    private static DateTime RefineTransition(
        SatelliteOrbit orbit,
        DateTime before,
        DateTime after,
        bool sunlitBefore)
    {
        var lo = before;
        var hi = after;

        while ((hi - lo) > RefineTolerance)
        {
            var mid = lo + (hi - lo) / 2;
            var midSunlit = IsSunlitAt(orbit, mid);
            if (midSunlit == sunlitBefore)
                lo = mid;
            else
                hi = mid;
        }

        return lo + (hi - lo) / 2;
    }

    private static bool IsSunlitAt(SatelliteOrbit orbit, DateTime time)
    {
        try
        {
            var sun = SunPositionCalculator.GetPosition(time);
            var sat = OrbitToolsMapping.ToEciPosition(orbit.PositionEci(time));
            return SatelliteIllumination.IsSunlit(sat, sun);
        }
        catch
        {
            return true;
        }
    }
}
