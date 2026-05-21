using OscarWatch.Core.Models;



namespace OscarWatch.Core.Geo;



public static class FootprintGeometry

{

    private const double EarthRadiusKm = 6371.0;

    private const double PoleLatitudeLimit = 89.9;



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

        if (ring.Count < 3)

            return [];



        if (footprintRadiusDeg > 0)

        {

            if (ContainsNorthPole(subpoint, footprintRadiusDeg))

                return ProjectNorthPolarCap(subpoint, ring, mapWidth, mapHeight);



            if (ContainsSouthPole(subpoint, footprintRadiusDeg))

                return ProjectSouthPolarCap(subpoint, ring, mapWidth, mapHeight);

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

            points.Add(EquirectangularProjection.GeoToPixel(

                p.LatitudeDeg, lon, mapWidth, mapHeight));

        }



        return points;

    }



    /// <summary>
    /// Polar cap: outer arc of the horizon ring (sorted by longitude), closed along the map rim.
    /// </summary>
    private static List<(double X, double Y)> ProjectNorthPolarCap(
        GeoCoordinate subpoint,
        IReadOnlyList<GeoCoordinate> ring,
        double mapWidth,
        double mapHeight) =>
        ProjectPolarCap(subpoint, ring, mapWidth, mapHeight, southCap: false);

    private static List<(double X, double Y)> ProjectSouthPolarCap(
        GeoCoordinate subpoint,
        IReadOnlyList<GeoCoordinate> ring,
        double mapWidth,
        double mapHeight) =>
        ProjectPolarCap(subpoint, ring, mapWidth, mapHeight, southCap: true);

    private static List<(double X, double Y)> ProjectPolarCap(
        GeoCoordinate subpoint,
        IReadOnlyList<GeoCoordinate> ring,
        double mapWidth,
        double mapHeight,
        bool southCap)
    {
        const double edgeToleranceDeg = 1.5;

        // Furthest from the enclosed pole = outer edge of the disc on the map.
        var outerLat = southCap
            ? ring.Max(p => p.LatitudeDeg)
            : ring.Min(p => p.LatitudeDeg);

        var arc = new List<(GeoCoordinate Geo, double LonNorm)>();
        foreach (var p in ring)
        {
            var onOuterEdge = southCap
                ? p.LatitudeDeg >= outerLat - edgeToleranceDeg
                : p.LatitudeDeg <= outerLat + edgeToleranceDeg;
            if (!onOuterEdge)
                continue;

            var lon = EquirectangularProjection.NormalizeLongitudeNear(
                p.LongitudeDeg, subpoint.LongitudeDeg);
            arc.Add((p, lon));
        }

        if (arc.Count < 3)
        {
            arc.Clear();
            foreach (var p in ring)
            {
                var lon = EquirectangularProjection.NormalizeLongitudeNear(
                    p.LongitudeDeg, subpoint.LongitudeDeg);
                arc.Add((p, lon));
            }
        }

        arc.Sort((a, b) => a.LonNorm.CompareTo(b.LonNorm));

        var boundary = new List<(double X, double Y)>(arc.Count);
        var minX = double.MaxValue;
        var maxX = double.MinValue;

        foreach (var (geo, lon) in arc)
        {
            var pt = GeoToPixelForCapArc(geo.LatitudeDeg, lon, mapWidth, mapHeight);
            boundary.Add(pt);
            minX = Math.Min(minX, pt.X);
            maxX = Math.Max(maxX, pt.X);
        }

        if (boundary.Count < 3 || minX >= maxX)
            return ProjectGeographicRing(subpoint, ring, mapWidth, mapHeight);

        var rimY = RimPixelY(mapHeight, southCap);
        return CloseCapAlongRim(boundary, minX, maxX, rimY, mapWidth);
    }

    /// <summary>Arc points use continuous latitude so the cap edge is not flattened onto the map rim.</summary>
    private static (double X, double Y) GeoToPixelForCapArc(
        double latDeg,
        double lonDeg,
        double width,
        double height)
    {
        var x = (lonDeg + 180.0) / 360.0 * width;
        var y = (90.0 - latDeg) / 180.0 * height;
        return (x, y);
    }

    private static double RimPixelY(double mapHeight, bool southCap) =>
        southCap ? mapHeight - 0.5 : 0.5;



    private static List<(double X, double Y)> CloseCapAlongRim(

        List<(double X, double Y)> boundary,

        double minX,

        double maxX,

        double rimY,

        double mapWidth)

    {

        var vertices = new List<(double X, double Y)>(boundary.Count + 24);

        vertices.AddRange(boundary);



        var first = boundary[0];

        var last = boundary[^1];



        AppendRimArcByX(vertices, last.X, maxX, rimY, mapWidth, longWay: false);

        AppendRimArcByX(vertices, maxX, minX, rimY, mapWidth, longWay: true);

        AppendRimArcByX(vertices, minX, first.X, rimY, mapWidth, longWay: false);



        return vertices.Count >= 3 ? vertices : boundary;

    }



    private static void AppendRimArcByX(

        List<(double X, double Y)> vertices,

        double x0,

        double x1,

        double rimY,

        double mapWidth,

        bool longWay)

    {

        if (Math.Abs(x0 - x1) < 0.5)

        {

            vertices.Add((x1, rimY));

            return;

        }



        var forward = x1 - x0;

        var wrap = forward > 0 ? forward - mapWidth : forward + mapWidth;

        var delta = longWay

            ? (Math.Abs(forward) >= Math.Abs(wrap) ? forward : wrap)

            : (Math.Abs(forward) <= Math.Abs(wrap) ? forward : wrap);

        var steps = Math.Clamp((int)(Math.Abs(delta) / (mapWidth * 0.02)), 2, 48);



        for (var s = 1; s <= steps; s++)

            vertices.Add((x0 + delta * s / steps, rimY));

    }



}


