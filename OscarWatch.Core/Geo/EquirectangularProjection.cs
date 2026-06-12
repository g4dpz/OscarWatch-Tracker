using OscarWatch.Core.Models;



namespace OscarWatch.Core.Geo;



public static class EquirectangularProjection

{

    private const double PoleLatitudeLimit = 89.9;



    public static (double X, double Y) GeoToPixel(double latDeg, double lonDeg, double width, double height)

    {

        var x = (lonDeg + 180.0) / 360.0 * width;

        var y = latDeg <= -PoleLatitudeLimit ? height

            : latDeg >= PoleLatitudeLimit ? 0.0

            : (90.0 - latDeg) / 180.0 * height;

        return (x, y);

    }



    public static IReadOnlyList<IReadOnlyList<(double X, double Y)>> SplitAtAntimeridian(

        IEnumerable<GeoCoordinate> points,

        double width,

        double height)

    {

        var segments = new List<List<(double X, double Y)>>();

        List<(double X, double Y)>? current = null;

        double? prevLon = null;



        foreach (var p in points)

        {

            var (x, y) = GeoToPixel(p.LatitudeDeg, p.LongitudeDeg, width, height);



            if (prevLon.HasValue && IsAntimeridianStep(prevLon.Value, p.LongitudeDeg))

            {

                if (current is { Count: > 0 })

                    segments.Add(current);

                current = new List<(double X, double Y)>();

            }



            current ??= new List<(double X, double Y)>();

            current.Add((x, y));

            prevLon = p.LongitudeDeg;

        }



        if (current is { Count: > 0 })

            segments.Add(current);



        return segments;

    }



    public static bool CrossesAntimeridian(IEnumerable<GeoCoordinate> points)

    {

        double? prevLon = null;

        foreach (var p in points)

        {

            if (prevLon.HasValue && IsAntimeridianStep(prevLon.Value, p.LongitudeDeg))

                return true;

            prevLon = p.LongitudeDeg;

        }



        return false;

    }



    public static IReadOnlyList<IReadOnlyList<(double X, double Y)>> SplitForMapDraw(

        IEnumerable<GeoCoordinate> points,

        double width,

        double height)

    {

        var maxDx = width / 2.0;

        var maxDy = height / 3.0;

        var chains = new List<List<(double X, double Y)>>();



        foreach (var segment in SplitAtAntimeridian(points, width, height))

        {

            List<(double X, double Y)>? chain = null;

            (double X, double Y)? prev = null;



            foreach (var p in segment)

            {

                if (prev is { } last &&

                    (Math.Abs(p.X - last.X) > maxDx || Math.Abs(p.Y - last.Y) > maxDy))

                {

                    if (chain is { Count: >= 2 })

                        chains.Add(chain);

                    chain = null;

                }



                chain ??= new List<(double X, double Y)>();

                chain.Add(p);

                prev = p;

            }



            if (chain is { Count: >= 2 })

                chains.Add(chain);

        }



        return chains;

    }

    /// <summary>
    /// Projects a ground track for map drawing. Longitudes are unwrapped along the path so
    /// polar orbits are not false-split by pixel jumps; chains break only at the antimeridian.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<(double X, double Y)>> ProjectGroundTrackForDraw(
        IReadOnlyList<GeoCoordinate> points,
        double width,
        double height)
    {
        if (points.Count < 2)
            return [];

        var segments = new List<List<(double X, double Y)>>();
        List<(double X, double Y)>? current = null;
        double unwrappedLon = points[0].LongitudeDeg;
        GeoCoordinate? prevPoint = null;
        double? prevRawLon = null;

        foreach (var p in points)
        {
            if (prevPoint is not null && prevRawLon.HasValue
                && IsAntimeridianStep(prevRawLon.Value, p.LongitudeDeg))
            {
                var crossLon = prevRawLon.Value > p.LongitudeDeg ? 180.0 : -180.0;
                var crossLat = InterpolateLatitudeAtLongitude(prevPoint, p, crossLon);
                var crossPixel = GeoToPixel(crossLat, crossLon, width, height);

                current ??= new List<(double X, double Y)>();
                current.Add(crossPixel);
                if (current.Count > 0)
                    segments.Add(current);

                current = new List<(double X, double Y)>();
                unwrappedLon = p.LongitudeDeg;
            }
            else if (prevRawLon is not null)
            {
                unwrappedLon += ShortestLongitudeDelta(unwrappedLon, p.LongitudeDeg);
            }

            var pixel = GeoToPixel(p.LatitudeDeg, unwrappedLon, width, height);
            current ??= new List<(double X, double Y)>();
            current.Add(pixel);
            prevPoint = p;
            prevRawLon = p.LongitudeDeg;
        }

        if (current is { Count: > 0 })
            segments.Add(current);

        return segments;
    }

    public static IReadOnlyList<GeoCoordinate> UnwrapNearLongitude(

        IEnumerable<GeoCoordinate> points,

        double centerLongitudeDeg)

    {

        var list = new List<GeoCoordinate>();

        foreach (var p in points)

        {

            list.Add(new GeoCoordinate(

                p.LatitudeDeg,

                NormalizeLongitudeNear(p.LongitudeDeg, centerLongitudeDeg),

                p.AltitudeKm));

        }



        return list;

    }



    public static double NormalizeLongitudeNear(double longitudeDeg, double centerLongitudeDeg)

    {

        var lon = longitudeDeg;

        var delta = lon - centerLongitudeDeg;

        while (delta > 180)

        {

            lon -= 360;

            delta = lon - centerLongitudeDeg;

        }



        while (delta < -180)

        {

            lon += 360;

            delta = lon - centerLongitudeDeg;

        }



        return lon;

    }

    private static bool IsAntimeridianStep(double fromLongitudeDeg, double toLongitudeDeg)
    {
        if (Math.Abs(toLongitudeDeg - fromLongitudeDeg) > 180)
            return true;

        return Math.Abs(ShortestLongitudeDelta(fromLongitudeDeg, toLongitudeDeg)) >= 179.5;
    }

    private static double ShortestLongitudeDelta(double fromLongitudeDeg, double toLongitudeDeg)
    {
        var delta = toLongitudeDeg - fromLongitudeDeg;
        while (delta > 180)
            delta -= 360;
        while (delta < -180)
            delta += 360;
        return delta;
    }

    private static double InterpolateLatitudeAtLongitude(
        GeoCoordinate a,
        GeoCoordinate b,
        double longitudeDeg)
    {
        var bLon = NormalizeLongitudeNear(b.LongitudeDeg, a.LongitudeDeg);
        var lon = NormalizeLongitudeNear(longitudeDeg, a.LongitudeDeg);
        var deltaLon = bLon - a.LongitudeDeg;
        if (Math.Abs(deltaLon) < 0.0001)
            return a.LatitudeDeg;

        var t = (lon - a.LongitudeDeg) / deltaLon;
        t = Math.Clamp(t, 0.0, 1.0);
        return a.LatitudeDeg + t * (b.LatitudeDeg - a.LatitudeDeg);
    }

}


