using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;

namespace OscarWatch.Orbit;

public sealed class SunlightSegmentPredictor : IIlluminationPredictor
{
    private static readonly TimeSpan CoarseStep = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RefineTolerance = TimeSpan.FromSeconds(1);

    private readonly IOrbitPropagator _propagator;

    public SunlightSegmentPredictor(IOrbitPropagator propagator)
    {
        _propagator = propagator;
    }

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

    private IReadOnlyList<IlluminationSegment> PredictCore(
        SatelliteCatalogEntry satellite,
        DateTime utcStart,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        var usePropagator = _propagator.HasSatellite(satellite.NoradId);
        var orbit = usePropagator ? null : OrbitToolsMapping.CreateOrbit(satellite);
        var utcEnd = utcStart + duration;
        var segments = new List<IlluminationSegment>();

        EciPosition GetSatEci(DateTime time)
        {
            return usePropagator
                ? _propagator.GetEciPosition(satellite.NoradId, time)
                : OrbitToolsMapping.ToEciPosition(orbit!.PositionEci(time));
        }

        (bool sunlit, EciPosition sunEci) IsSunlitWithSun(DateTime time)
        {
            try
            {
                var sun = SunPositionCalculator.GetPosition(time);
                var sat = GetSatEci(time);
                return (SatelliteIllumination.IsSunlit(sat, sun), sun);
            }
            catch
            {
                return (true, default);
            }
        }

        bool IsSunlitAt(DateTime time, EciPosition sun)
        {
            try
            {
                var sat = GetSatEci(time);
                return SatelliteIllumination.IsSunlit(sat, sun);
            }
            catch
            {
                return true;
            }
        }

        DateTime RefineTransition(DateTime before, DateTime after, bool sunlitBefore, EciPosition sunAtBefore)
        {
            var lo = before;
            var hi = after;

            while ((hi - lo) > RefineTolerance)
            {
                var mid = lo + (hi - lo) / 2;
                // Sun at 'mid' must be freshly computed (sun moves ~0.04°/min).
                var midSun = SunPositionCalculator.GetPosition(mid);
                var midSunlit = IsSunlitAt(mid, midSun);
                if (midSunlit == sunlitBefore)
                    lo = mid;
                else
                    hi = mid;
            }

            return lo + (hi - lo) / 2;
        }

        var t = utcStart;
        var (currentSunlit, previousSunEci) = IsSunlitWithSun(t);
        var segmentStart = t;

        while (t <= utcEnd)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (sunlit, sunEci) = IsSunlitWithSun(t);
            if (sunlit != currentSunlit)
            {
                // Pass the cached sun position from t - CoarseStep to avoid recomputing it
                var boundary = RefineTransition(t - CoarseStep, t, currentSunlit, previousSunEci);
                segments.Add(new IlluminationSegment
                {
                    StartUtc = segmentStart,
                    EndUtc = boundary,
                    IsSunlit = currentSunlit
                });
                segmentStart = boundary;
                currentSunlit = sunlit;
            }

            previousSunEci = sunEci;
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
}
