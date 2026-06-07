using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Core.Tle;

namespace OscarWatch.Orbit;

public sealed class SampledGroundGeometry : IGroundGeometry
{
    private readonly IOrbitPropagator _propagator;

    public SampledGroundGeometry(IOrbitPropagator propagator)
    {
        _propagator = propagator;
    }

    public IReadOnlyList<GeoCoordinate> GetGroundTrack(
        SatelliteCatalogEntry satellite,
        DateTime utcStart,
        DateTime utcEnd,
        TimeSpan step)
    {
        var usePropagator = _propagator.HasSatellite(satellite.NoradId);
        var orbit = usePropagator ? null : OrbitToolsMapping.CreateOrbit(satellite);
        var points = new List<GeoCoordinate>();

        for (var t = utcStart; t <= utcEnd; t += step)
        {
            try
            {
                var point = usePropagator
                    ? _propagator.GetSubpoint(satellite.NoradId, t)
                    : OrbitToolsMapping.ToGeoCoordinate(orbit!.PositionEci(t));
                points.Add(point);
            }
            catch
            {
                // skip decayed points
            }
        }

        return points;
    }

    public IReadOnlyList<GeoCoordinate> GetFootprint(
        SatelliteCatalogEntry satellite,
        DateTime utc,
        double minimumElevationDeg)
    {
        var usePropagator = _propagator.HasSatellite(satellite.NoradId);

        GeoCoordinate subpoint;
        try
        {
            subpoint = usePropagator
                ? _propagator.GetSubpoint(satellite.NoradId, utc)
                : OrbitToolsMapping.ToGeoCoordinate(
                    OrbitToolsMapping.CreateOrbit(satellite).PositionEci(utc));
        }
        catch
        {
            return [];
        }

        var altKm = TleAltitude.ResolveAltitudeKm(subpoint.AltitudeKm, satellite);
        var radiusDeg = FootprintGeometry.HorizonRadiusDeg(altKm, minimumElevationDeg);
        if (radiusDeg <= 0)
            return [];
        const int samples = 90;
        var ring = new List<GeoCoordinate>(samples);

        for (var i = 0; i < samples; i++)
        {
            var bearing = i * 360.0 / samples;
            var (lat, lon) = SphericalGeo.DestinationPoint(
                subpoint.LatitudeDeg,
                subpoint.LongitudeDeg,
                radiusDeg,
                bearing);
            ring.Add(new GeoCoordinate(lat, lon));

        }

        return ring;
    }
}
