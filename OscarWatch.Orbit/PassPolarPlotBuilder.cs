using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;

namespace OscarWatch.Orbit;

public static class PassPolarPlotBuilder
{
    private static readonly TimeSpan SampleStep = TimeSpan.FromSeconds(15);

    public static PassPolarPlotData Build(
        SatelliteCatalogEntry satellite,
        IOrbitPropagator propagator,
        GroundStation site,
        PassInfo pass,
        bool useFullPass,
        DateTime mutualStartUtc,
        DateTime mutualEndUtc,
        double minimumElevationDeg = 0)
    {
        var plotStart = useFullPass ? pass.AosUtc : Max(pass.AosUtc, mutualStartUtc);
        var plotEnd = useFullPass ? pass.LosUtc : Min(pass.LosUtc, mutualEndUtc);
        if (plotEnd <= plotStart)
        {
            plotStart = pass.AosUtc;
            plotEnd = pass.LosUtc;
        }

        var samples = SamplePass(satellite, propagator, site, plotStart, plotEnd, minimumElevationDeg);
        var segments = BuildSegments(samples);
        var (aosAz, losAz, maxEl) = ComputePlotStats(
            satellite,
            propagator,
            site,
            pass,
            plotStart,
            plotEnd,
            samples,
            useFullPass);

        return new PassPolarPlotData
        {
            StationLabel = string.IsNullOrWhiteSpace(site.GridSquare)
                ? site.DisplayName
                : site.GridSquare.ToUpperInvariant(),
            AosAzimuthDeg = aosAz,
            LosAzimuthDeg = losAz,
            MaxElevationDeg = maxEl,
            Segments = segments,
            Samples = samples.Select(s => new PassPolarPlotSample
            {
                Utc = s.Utc,
                AzimuthDeg = s.AzimuthDeg,
                ElevationDeg = s.ElevationDeg
            }).ToArray(),
            MutualStart = TryMarkerAt(satellite, propagator, site, mutualStartUtc, PassPolarPlotMarkerKind.MutualWindowStart, minimumElevationDeg),
            MutualEnd = TryMarkerAt(satellite, propagator, site, mutualEndUtc, PassPolarPlotMarkerKind.MutualWindowEnd, minimumElevationDeg)
        };
    }

    private static List<(DateTime Utc, double AzimuthDeg, double ElevationDeg, bool IsSunlit)> SamplePass(
        SatelliteCatalogEntry satellite,
        IOrbitPropagator propagator,
        GroundStation site,
        DateTime startUtc,
        DateTime endUtc,
        double minimumElevationDeg)
    {
        var samples = new List<(DateTime, double, double, bool)>();
        for (var t = startUtc; t <= endUtc; t += SampleStep)
        {
            var look = propagator.GetLookAngles(satellite.NoradId, site, t);
            if (look.ElevationDeg < minimumElevationDeg)
                continue;

            var satEci = propagator.GetEciPosition(satellite.NoradId, t);
            var sunEci = SunPositionCalculator.GetPosition(t);
            var sunlit = SatelliteIllumination.IsSunlit(satEci, sunEci);
            samples.Add((t, look.AzimuthDeg, look.ElevationDeg, sunlit));
        }

        if (samples.Count == 0 || samples[^1].Item1 < endUtc)
        {
            var look = propagator.GetLookAngles(satellite.NoradId, site, endUtc);
            if (look.ElevationDeg >= minimumElevationDeg)
            {
                var satEci = propagator.GetEciPosition(satellite.NoradId, endUtc);
                var sunEci = SunPositionCalculator.GetPosition(endUtc);
                samples.Add((endUtc, look.AzimuthDeg, look.ElevationDeg, SatelliteIllumination.IsSunlit(satEci, sunEci)));
            }
        }

        return samples;
    }

    private static IReadOnlyList<PassPolarPlotSegment> BuildSegments(
        List<(DateTime Utc, double AzimuthDeg, double ElevationDeg, bool IsSunlit)> samples)
    {
        if (samples.Count == 0)
            return [];

        var segments = new List<PassPolarPlotSegment>();
        var currentSunlit = samples[0].IsSunlit;
        var currentPoints = new List<(double, double)> { (samples[0].AzimuthDeg, samples[0].ElevationDeg) };

        for (var i = 1; i < samples.Count; i++)
        {
            var sample = samples[i];
            if (sample.IsSunlit == currentSunlit)
            {
                currentPoints.Add((sample.AzimuthDeg, sample.ElevationDeg));
                continue;
            }

            segments.Add(new PassPolarPlotSegment
            {
                IsSunlit = currentSunlit,
                Points = currentPoints.ToArray()
            });
            currentSunlit = sample.IsSunlit;
            currentPoints = [(sample.AzimuthDeg, sample.ElevationDeg)];
        }

        segments.Add(new PassPolarPlotSegment
        {
            IsSunlit = currentSunlit,
            Points = currentPoints.ToArray()
        });

        return segments;
    }

    private static (double AosAzimuthDeg, double LosAzimuthDeg, double MaxElevationDeg) ComputePlotStats(
        SatelliteCatalogEntry satellite,
        IOrbitPropagator propagator,
        GroundStation site,
        PassInfo pass,
        DateTime plotStart,
        DateTime plotEnd,
        List<(DateTime Utc, double AzimuthDeg, double ElevationDeg, bool IsSunlit)> samples,
        bool useFullPass)
    {
        if (useFullPass)
            return (pass.AosAzimuthDeg, pass.LosAzimuthDeg, pass.MaxElevationDeg);

        var aos = propagator.GetLookAngles(satellite.NoradId, site, plotStart);
        var los = propagator.GetLookAngles(satellite.NoradId, site, plotEnd);
        var maxEl = samples.Count > 0
            ? samples.Max(s => s.ElevationDeg)
            : 0.0;

        return (aos.AzimuthDeg, los.AzimuthDeg, maxEl);
    }

    private static PassPolarPlotMarker? TryMarkerAt(
        SatelliteCatalogEntry satellite,
        IOrbitPropagator propagator,
        GroundStation site,
        DateTime utc,
        PassPolarPlotMarkerKind kind,
        double minimumElevationDeg)
    {
        var look = propagator.GetLookAngles(satellite.NoradId, site, utc);
        if (look.ElevationDeg < minimumElevationDeg)
            return null;

        return new PassPolarPlotMarker
        {
            AzimuthDeg = look.AzimuthDeg,
            ElevationDeg = look.ElevationDeg,
            Kind = kind
        };
    }

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
}
