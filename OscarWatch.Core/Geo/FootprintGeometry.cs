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
        double mapHeight,
        double centerLongitudeDeg = 0)
    {
        if (ring.Count < 3 || mapWidth <= 0 || mapHeight <= 0)
            return [];

        if (footprintRadiusDeg > 0)
        {
            if (ContainsNorthPole(subpoint, footprintRadiusDeg))
                return ProjectPolarCap(ring, mapWidth, mapHeight, southCap: false, centerLongitudeDeg);

            if (ContainsSouthPole(subpoint, footprintRadiusDeg))
                return ProjectPolarCap(ring, mapWidth, mapHeight, southCap: true, centerLongitudeDeg);
        }

        return ProjectGeographicRing(subpoint, ring, mapWidth, mapHeight, centerLongitudeDeg);
    }

    private static List<(double X, double Y)> ProjectGeographicRing(
        GeoCoordinate subpoint,
        IReadOnlyList<GeoCoordinate> ring,
        double mapWidth,
        double mapHeight,
        double centerLongitudeDeg)
    {
        var points = new List<(double X, double Y)>(ring.Count);
        foreach (var p in ring)
        {
            var lon = EquirectangularProjection.NormalizeLongitudeNear(
                p.LongitudeDeg, subpoint.LongitudeDeg);
            points.Add(EquirectangularProjection.GeoToPixel(
                p.LatitudeDeg, lon, mapWidth, mapHeight, centerLongitudeDeg));
        }

        return points;
    }

    /// <summary>
    /// Polar-cap polygon. When the horizon ring encloses a pole, walking the ring in bearing
    /// order sweeps every longitude exactly once. Longitudes are wrapped relative to the map
    /// centre so the viewport seam (centre ± 180°) maps to the left/right edges; sorting by x
    /// then closing along the pole rim stays valid for any centre.
    /// </summary>
    private static List<(double X, double Y)> ProjectPolarCap(
        IReadOnlyList<GeoCoordinate> ring,
        double mapWidth,
        double mapHeight,
        bool southCap,
        double centerLongitudeDeg)
    {
        var boundary = new List<(double X, double Y)>(ring.Count);
        foreach (var p in ring)
        {
            var lon = EquirectangularProjection.NormalizeLongitudeNear(
                p.LongitudeDeg, centerLongitudeDeg);
            var (x, y) = EquirectangularProjection.GeoToPixel(
                p.LatitudeDeg, lon, mapWidth, mapHeight, centerLongitudeDeg);
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
}
