using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Core.Tle;

namespace OscarWatch.Orbit;

public sealed class SampledGroundGeometry : IGroundGeometry
{
    public IReadOnlyList<GeoCoordinate> GetGroundTrack(
        SatelliteCatalogEntry satellite,
        DateTime utcStart,
        DateTime utcEnd,
        TimeSpan step)
    {
        var orbit = OrbitToolsMapping.CreateOrbit(satellite);
        var points = new List<GeoCoordinate>();

        for (var t = utcStart; t <= utcEnd; t += step)
        {
            try
            {
                points.Add(OrbitToolsMapping.ToGeoCoordinate(orbit.PositionEci(t)));
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
        var orbit = OrbitToolsMapping.CreateOrbit(satellite);

        GeoCoordinate subpoint;
        try
        {
            subpoint = OrbitToolsMapping.ToGeoCoordinate(orbit.PositionEci(utc));
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
