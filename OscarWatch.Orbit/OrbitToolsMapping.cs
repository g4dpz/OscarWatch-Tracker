using OscarWatch.Core.Models;
using Zeptomoby.OrbitTools;
using SatelliteOrbit = Zeptomoby.OrbitTools.Orbit;

namespace OscarWatch.Orbit;

internal static class OrbitToolsMapping
{
    public static Tle CreateTle(SatelliteCatalogEntry entry) =>
        new(entry.Name, entry.Line1, entry.Line2);

    public static SatelliteOrbit CreateOrbit(SatelliteCatalogEntry entry) =>
        new(CreateTle(entry));

    public static EciPosition ToEciPosition(EciTime eci) =>
        new(eci.Position.X, eci.Position.Y, eci.Position.Z);

    public static GeoCoordinate ToGeoCoordinate(EciTime eci)
    {
        var geo = new GeoTime(eci);
        return new GeoCoordinate(geo.LatitudeDeg, geo.LongitudeDeg, geo.Altitude);
    }

    public static LookAngles ToLookAngles(TopoTime topo, double rangeRateKmPerSec = 0) =>
        new(topo.AzimuthDeg, topo.ElevationDeg, topo.Range, rangeRateKmPerSec);

    public static Site CreateSite(GroundStation station) =>
        new(station.LatitudeDeg, station.LongitudeDeg, station.AltitudeKm);
}
