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



            if (prevLon.HasValue && Math.Abs(p.LongitudeDeg - prevLon.Value) > 180)

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

            if (prevLon.HasValue && Math.Abs(p.LongitudeDeg - prevLon.Value) > 180)

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

}


