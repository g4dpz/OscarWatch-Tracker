using OscarWatch.Core.Models;

namespace OscarWatch.Core.Geo;

public static class FootprintGeometry
{
    private const double EarthRadiusKm = 6371.0;

    /// <summary>
    /// The ring wraps around a pole only when the angular distance from the subpoint to that
    /// pole is strictly less than the footprint radius, i.e. lat + radius &gt; 90°.  Using 90°
    /// as the threshold (with ≥) is the exact mathematical condition; values below 90° would
    /// trigger the polar-cap projection for rings that don't actually encircle the pole,
    /// producing wide horizontal-band artefacts on the map.
    /// </summary>
    private const double PoleLatitudeLimit = 90.0;

    public static bool ContainsNorthPole(GeoCoordinate subpoint, double footprintRadiusDeg) =>
        subpoint.LatitudeDeg + footprintRadiusDeg >= PoleLatitudeLimit;

    public static bool ContainsSouthPole(GeoCoordinate subpoint, double footprintRadiusDeg) =>
        subpoint.LatitudeDeg - footprintRadiusDeg <= -PoleLatitudeLimit;

    public static double EstimateRingRadiusDeg(GeoCoordinate subpoint, IReadOnlyList<GeoCoordinate> ring)
    {
        var maxDeg = 0.0;
        var lat0 = subpoint.LatitudeDeg * Math.PI / 180.0;
        foreach (var p in ring)
        {
            var dLat = (p.LatitudeDeg - subpoint.LatitudeDeg) * Math.PI / 180.0;
            var dLon = (p.LongitudeDeg - subpoint.LongitudeDeg) * Math.PI / 180.0;
            var dist = Math.Sqrt(
                dLat * dLat + dLon * dLon * Math.Cos(lat0) * Math.Cos(lat0)) * 180.0 / Math.PI;
            if (dist > maxDeg)
                maxDeg = dist;
        }

        return maxDeg;
    }

    public static double HorizonRadiusDeg(double altitudeKm, double minimumElevationDeg = 0)
    {
        if (altitudeKm <= 0)
            return 0;

        var ratio = EarthRadiusKm / (EarthRadiusKm + altitudeKm);
        var horizonRad = Math.Acos(Math.Clamp(ratio, -1, 1));
        var minElRad = minimumElevationDeg * Math.PI / 180.0;
        var footprintRad = horizonRad - minElRad;
        return footprintRad > 0 ? footprintRad * 180.0 / Math.PI : 0;
    }

    public static IReadOnlyList<(double X, double Y)> ProjectRingToMap(
        GeoCoordinate subpoint,
        IReadOnlyList<GeoCoordinate> ring,
        double footprintRadiusDeg,
        double mapWidth,
        double mapHeight)
    {
        if (ring.Count < 3 || mapWidth <= 0 || mapHeight <= 0)
            return [];

        if (footprintRadiusDeg > 0)
        {
            if (ContainsNorthPole(subpoint, footprintRadiusDeg))
                return ProjectPolarCap(ring, mapWidth, mapHeight, southCap: false);

            if (ContainsSouthPole(subpoint, footprintRadiusDeg))
                return ProjectPolarCap(ring, mapWidth, mapHeight, southCap: true);
        }

        return ProjectGeographicRing(subpoint, ring, mapWidth, mapHeight);
    }

    private static List<(double X, double Y)> ProjectGeographicRing(
        GeoCoordinate subpoint,
        IReadOnlyList<GeoCoordinate> ring,
        double mapWidth,
        double mapHeight)
    {
        var points = new List<(double X, double Y)>(ring.Count);
        foreach (var p in ring)
        {
            var lon = EquirectangularProjection.NormalizeLongitudeNear(
                p.LongitudeDeg, subpoint.LongitudeDeg);
            points.Add(EquirectangularProjection.GeoToPixel(p.LatitudeDeg, lon, mapWidth, mapHeight));
        }

        return points;
    }

    /// <summary>
    /// Polar-cap polygon. When the horizon ring encloses a pole, walking the ring in bearing
    /// order sweeps every longitude exactly once, so each longitude in [-180°, +180°] maps to
    /// a single boundary latitude. We sort the projected ring points by x (==longitude order)
    /// to get a monotone left-to-right boundary, then close the polygon along the map's pole
    /// rim. The two ring endpoints (at lon ≈ ±180°) are the same physical point, so the polygon
    /// closes cleanly across the antimeridian.
    /// </summary>
    private static List<(double X, double Y)> ProjectPolarCap(
        IReadOnlyList<GeoCoordinate> ring,
        double mapWidth,
        double mapHeight,
        bool southCap)
    {
        var boundary = new List<(double X, double Y)>(ring.Count);
        foreach (var p in ring)
        {
            var lon = WrapLongitudeToCanonical(p.LongitudeDeg);
            var x = (lon + 180.0) / 360.0 * mapWidth;
            var y = LatitudeToY(p.LatitudeDeg, mapHeight);
            boundary.Add((x, y));
        }

        boundary.Sort(static (a, b) => a.X.CompareTo(b.X));

        var rimY = southCap ? mapHeight - 0.5 : 0.5;
        var leftEdgeY = boundary[0].Y;
        var rightEdgeY = boundary[^1].Y;

        // Polygon order (north cap shown; south is mirrored to the bottom rim):
        //   (0, rimY) → (0, leftEdgeY) → boundary left→right → (mapWidth, rightEdgeY) → (mapWidth, rimY)
        // implicitly closes back to (0, rimY) along the rim.
        var polygon = new List<(double X, double Y)>(boundary.Count + 4)
        {
            (0.0, rimY),
            (0.0, leftEdgeY)
        };
        polygon.AddRange(boundary);
        polygon.Add((mapWidth, rightEdgeY));
        polygon.Add((mapWidth, rimY));

        return polygon;
    }

    private static double LatitudeToY(double latDeg, double height)
    {
        var clamped = Math.Clamp(latDeg, -90.0, 90.0);
        return (90.0 - clamped) / 180.0 * height;
    }

    private static double WrapLongitudeToCanonical(double lonDeg)
    {
        var lon = lonDeg % 360.0;
        if (lon > 180.0)
            lon -= 360.0;
        else if (lon < -180.0)
            lon += 360.0;
        return lon;
    }
}
