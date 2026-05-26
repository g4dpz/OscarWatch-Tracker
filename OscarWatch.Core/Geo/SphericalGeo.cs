namespace OscarWatch.Core.Geo;

public static class SphericalGeo
{
    /// <summary>Great-circle distance in degrees between two surface points.</summary>
    public static double AngularDistanceDeg(
        double lat1Deg,
        double lon1Deg,
        double lat2Deg,
        double lon2Deg)
    {
        var lat1 = lat1Deg * Math.PI / 180.0;
        var lat2 = lat2Deg * Math.PI / 180.0;
        var dLon = (lon2Deg - lon1Deg) * Math.PI / 180.0;

        var cosD = Math.Sin(lat1) * Math.Sin(lat2) +
            Math.Cos(lat1) * Math.Cos(lat2) * Math.Cos(dLon);

        cosD = Math.Clamp(cosD, -1.0, 1.0);
        return Math.Acos(cosD) * 180.0 / Math.PI;
    }

    /// <summary>Initial great-circle bearing from point 1 to point 2 (degrees, 0 = north).</summary>
    public static double InitialBearingDeg(
        double lat1Deg,
        double lon1Deg,
        double lat2Deg,
        double lon2Deg)
    {
        var lat1 = lat1Deg * Math.PI / 180.0;
        var lat2 = lat2Deg * Math.PI / 180.0;
        var dLon = (lon2Deg - lon1Deg) * Math.PI / 180.0;

        var y = Math.Sin(dLon) * Math.Cos(lat2);
        var x = Math.Cos(lat1) * Math.Sin(lat2) -
            Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);

        return (Math.Atan2(y, x) * 180.0 / Math.PI + 360.0) % 360.0;
    }

    public static (double Lat, double Lon) DestinationPoint(
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
