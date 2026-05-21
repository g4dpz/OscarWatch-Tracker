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
}
