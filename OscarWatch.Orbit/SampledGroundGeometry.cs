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
            var (lat, lon) = DestinationPoint(
                subpoint.LatitudeDeg,
                subpoint.LongitudeDeg,
                radiusDeg,
                bearing);
            ring.Add(new GeoCoordinate(lat, lon));

        }

        return ring;
    }

    private static (double Lat, double Lon) DestinationPoint(
        double latDeg,
        double lonDeg,
        double distanceDeg,
        double bearingDeg)
    {
        var lat1 = latDeg * Math.PI / 180.0;
        var lon1 = lonDeg * Math.PI / 180.0;
        var brng = bearingDeg * Math.PI / 180.0;
        var d = distanceDeg * Math.PI / 180.0;

        var lat2 = Math.Asin(
            Math.Sin(lat1) * Math.Cos(d) +
            Math.Cos(lat1) * Math.Sin(d) * Math.Cos(brng));

        var lon2 = lon1 + Math.Atan2(
            Math.Sin(brng) * Math.Sin(d) * Math.Cos(lat1),
            Math.Cos(d) - Math.Sin(lat1) * Math.Sin(lat2));

        return (lat2 * 180.0 / Math.PI, lon2 * 180.0 / Math.PI);
    }

}
