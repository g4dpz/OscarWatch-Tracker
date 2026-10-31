using OscarWatch.Core.Models;

namespace OscarWatch.Core.Geo;

/// <summary>Geometry helpers for the Earth View that are independent of any drawing code.</summary>
public static class GlobeViewMath
{
    /// <summary>
    /// Angular radius, in degrees, of the area from which a satellite at <paramref name="altitudeKm"/>
    /// is above <paramref name="minimumElevationDeg"/>. Zero when the altitude is not positive.
    /// </summary>
    public static double FootprintRadiusDeg(double altitudeKm, double minimumElevationDeg) =>
        double.IsFinite(altitudeKm) && double.IsFinite(minimumElevationDeg)
            ? FootprintGeometry.HorizonRadiusDeg(altitudeKm, minimumElevationDeg)
            : 0;

    /// <summary>
    /// A closed ring of points at a fixed angular distance from the subpoint, in bearing order.
    /// The first point is not repeated at the end. Empty when the radius is not positive.
    /// </summary>
    public static IReadOnlyList<GeoCoordinate> FootprintRing(
        GeoCoordinate subpoint, double radiusDeg, int points = 180)
    {
        if (!double.IsFinite(radiusDeg) || radiusDeg <= 0 || points < 3)
            return [];

        var ring = new List<GeoCoordinate>(points);
        for (var i = 0; i < points; i++)
        {
            var bearingDeg = 360.0 * i / points;
            var (lat, lon) = Destination(subpoint.LatitudeDeg, subpoint.LongitudeDeg, radiusDeg, bearingDeg);
            ring.Add(new GeoCoordinate(lat, lon));
        }

        return ring;
    }

    /// <summary>True when the station is within <paramref name="radiusDeg"/> of the subpoint on the surface.</summary>
    public static bool IsInsideFootprint(
        GeoCoordinate subpoint, double stationLatDeg, double stationLonDeg, double radiusDeg)
    {
        if (!double.IsFinite(radiusDeg) || radiusDeg <= 0
            || !double.IsFinite(stationLatDeg) || !double.IsFinite(stationLonDeg))
            return false;

        return CentralAngleDeg(subpoint.LatitudeDeg, subpoint.LongitudeDeg, stationLatDeg, stationLonDeg) <= radiusDeg;
    }

    /// <summary>Great-circle angle between two points, in degrees.</summary>
    public static double CentralAngleDeg(double lat1Deg, double lon1Deg, double lat2Deg, double lon2Deg)
    {
        var lat1 = lat1Deg * Math.PI / 180.0;
        var lat2 = lat2Deg * Math.PI / 180.0;
        var dLon = (lon2Deg - lon1Deg) * Math.PI / 180.0;

        // The atan2 form stays accurate for very small and very large angles, unlike acos.
        var sinLat1 = Math.Sin(lat1);
        var cosLat1 = Math.Cos(lat1);
        var sinLat2 = Math.Sin(lat2);
        var cosLat2 = Math.Cos(lat2);
        var cosD = Math.Cos(dLon);
        var sinD = Math.Sin(dLon);

        var y = Math.Sqrt(
            Math.Pow(cosLat2 * sinD, 2) +
            Math.Pow(cosLat1 * sinLat2 - sinLat1 * cosLat2 * cosD, 2));
        var x = sinLat1 * sinLat2 + cosLat1 * cosLat2 * cosD;
        return Math.Atan2(y, x) * 180.0 / Math.PI;
    }

    /// <summary>
    /// Projects a ground track onto the globe and splits it into runs of points that are on the
    /// visible hemisphere. A run ends where the track passes the limb or where the input has a
    /// <see cref="double.NaN"/> latitude (the gap marker used by the ground track code), and a new
    /// run starts when it comes back into view. Runs with fewer than two points are dropped, as they
    /// cannot be drawn as a line. Coordinates are in Earth radii on the projection plane.
    /// </summary>
    /// <remarks>
    /// Runs end at the last visible sample rather than at the exact limb crossing. At the usual
    /// sampling interval this leaves a gap well under a pixel at normal window sizes.
    /// </remarks>
    public static IReadOnlyList<IReadOnlyList<(double X, double Y)>> ProjectTrack(
        GlobeCamera camera, IReadOnlyList<GeoCoordinate> track)
    {
        var basis = new GlobeBasis(camera);
        if (!basis.IsValid || track.Count < 2)
            return [];

        var runs = new List<IReadOnlyList<(double X, double Y)>>();
        List<(double X, double Y)>? current = null;

        foreach (var p in track)
        {
            var projected = double.IsNaN(p.LatitudeDeg)
                ? new GlobePoint(0, 0, false)
                : basis.Project(p.LatitudeDeg, p.LongitudeDeg);

            if (projected.Visible)
            {
                current ??= [];
                current.Add((projected.X, projected.Y));
            }
            else
            {
                CloseRun();
            }
        }

        CloseRun();
        return runs;

        void CloseRun()
        {
            if (current is { Count: >= 2 })
                runs.Add(current);
            current = null;
        }
    }

    /// <summary>Great-circle destination from a start point, an angular distance and a bearing.</summary>
    public static (double LatDeg, double LonDeg) Destination(
        double latDeg, double lonDeg, double distanceDeg, double bearingDeg)
    {
        var lat = latDeg * Math.PI / 180.0;
        var lon = lonDeg * Math.PI / 180.0;
        var d = distanceDeg * Math.PI / 180.0;
        var b = bearingDeg * Math.PI / 180.0;

        var lat2 = Math.Asin(Math.Clamp(
            Math.Sin(lat) * Math.Cos(d) + Math.Cos(lat) * Math.Sin(d) * Math.Cos(b), -1.0, 1.0));
        var lon2 = lon + Math.Atan2(
            Math.Sin(b) * Math.Sin(d) * Math.Cos(lat),
            Math.Cos(d) - Math.Sin(lat) * Math.Sin(lat2));

        var lonOutDeg = lon2 * 180.0 / Math.PI;
        lonOutDeg = (lonOutDeg + 540.0) % 360.0 - 180.0; // wrap to [-180, 180)
        return (lat2 * 180.0 / Math.PI, lonOutDeg);
    }
}
