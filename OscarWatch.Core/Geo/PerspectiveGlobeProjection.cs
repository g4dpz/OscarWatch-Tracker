namespace OscarWatch.Core.Geo;

/// <summary>Viewpoint above the Earth: the satellite's subpoint and altitude.</summary>
public readonly record struct GlobeCamera(
    double SubpointLatitudeDeg,
    double SubpointLongitudeDeg,
    double AltitudeKm);

/// <summary>
/// A point on the projection plane, in Earth radii. X is to the right (east at the centre),
/// Y is up (north at the centre), and the origin is the subpoint.
/// <see cref="Visible"/> is false when the point is beyond the limb.
/// </summary>
public readonly record struct GlobePoint(double X, double Y, bool Visible);

/// <summary>
/// True perspective projection of the Earth as seen from a satellite (the general perspective
/// projection in Snyder, Map Projections: A Working Manual). The image plane is tangent to the
/// globe at the subpoint, and all plane coordinates are in Earth radii.
/// </summary>
public static class PerspectiveGlobeProjection
{
    public const double EarthRadiusKm = 6371.0;

    /// <summary>Camera distance from the Earth's centre as a multiple of the Earth radius (P).</summary>
    public static double Ratio(double altitudeKm) => (EarthRadiusKm + altitudeKm) / EarthRadiusKm;

    /// <summary>
    /// Radius of the limb circle on the projection plane, in Earth radii: sqrt((P - 1) / (P + 1)).
    /// Zero when the altitude is not a finite positive number.
    /// </summary>
    public static double LimbRadius(double altitudeKm)
    {
        if (!double.IsFinite(altitudeKm) || altitudeKm <= 0)
            return 0;

        var p = Ratio(altitudeKm);
        return Math.Sqrt((p - 1.0) / (p + 1.0));
    }

    /// <summary>Projects a geographic point. See <see cref="GlobeBasis"/> to project many points.</summary>
    public static GlobePoint Project(GlobeCamera camera, double latDeg, double lonDeg) =>
        new GlobeBasis(camera).Project(latDeg, lonDeg);

    /// <summary>
    /// Inverse projection. Returns false when the plane point misses the globe, or when the camera
    /// altitude is not a finite positive number.
    /// </summary>
    public static bool TryUnproject(
        GlobeCamera camera, double x, double y, out double latDeg, out double lonDeg) =>
        new GlobeBasis(camera).TryUnproject(x, y, out latDeg, out lonDeg);
}

/// <summary>
/// Precomputed trigonometry for one camera, so that many points (a ground track, or every pixel of
/// a raster) can be projected without repeating it. Local axes: X east, Y north, Z up (towards the
/// satellite) at the subpoint.
/// </summary>
public readonly struct GlobeBasis
{
    private readonly double _sinLat0;
    private readonly double _cosLat0;
    private readonly double _lon0Rad;
    private readonly double _p;

    // Geographic (Earth-fixed) unit vectors of the local axes.
    private readonly double _eastX, _eastY;
    private readonly double _northX, _northY, _northZ;
    private readonly double _upX, _upY, _upZ;

    public GlobeBasis(GlobeCamera camera)
    {
        var valid = double.IsFinite(camera.SubpointLatitudeDeg)
            && double.IsFinite(camera.SubpointLongitudeDeg)
            && double.IsFinite(camera.AltitudeKm)
            && camera.AltitudeKm > 0;

        IsValid = valid;
        if (!valid)
        {
            _sinLat0 = _cosLat0 = _lon0Rad = _p = 0;
            _eastX = _eastY = _northX = _northY = _northZ = _upX = _upY = _upZ = 0;
            return;
        }

        var lat0 = camera.SubpointLatitudeDeg * Math.PI / 180.0;
        _lon0Rad = camera.SubpointLongitudeDeg * Math.PI / 180.0;
        _sinLat0 = Math.Sin(lat0);
        _cosLat0 = Math.Cos(lat0);
        _p = PerspectiveGlobeProjection.Ratio(camera.AltitudeKm);

        var sinLon0 = Math.Sin(_lon0Rad);
        var cosLon0 = Math.Cos(_lon0Rad);
        _eastX = -sinLon0;
        _eastY = cosLon0;
        _northX = -_sinLat0 * cosLon0;
        _northY = -_sinLat0 * sinLon0;
        _northZ = _cosLat0;
        _upX = _cosLat0 * cosLon0;
        _upY = _cosLat0 * sinLon0;
        _upZ = _sinLat0;
    }

    /// <summary>False when the camera altitude or position is not usable. Every method then reports a miss.</summary>
    public bool IsValid { get; }

    /// <summary>Radius of the limb circle on the projection plane, in Earth radii.</summary>
    public double LimbRadius => IsValid ? Math.Sqrt((_p - 1.0) / (_p + 1.0)) : 0;

    public GlobePoint Project(double latDeg, double lonDeg)
    {
        if (!IsValid)
            return new GlobePoint(0, 0, false);

        var lat = latDeg * Math.PI / 180.0;
        var dLon = lonDeg * Math.PI / 180.0 - _lon0Rad;
        var sinLat = Math.Sin(lat);
        var cosLat = Math.Cos(lat);
        var cosD = Math.Cos(dLon);

        // Local components of the unit vector to the point.
        var lx = cosLat * Math.Sin(dLon);
        var ly = _cosLat0 * sinLat - _sinLat0 * cosLat * cosD;
        var lz = _sinLat0 * sinLat + _cosLat0 * cosLat * cosD; // cos of the central angle

        // Visible when the point is above the satellite's horizon: cos c >= R / (R + h).
        // Written so that NaN input reports not visible.
        var visible = lz >= 1.0 / _p;
        var k = (_p - 1.0) / (_p - lz);
        return new GlobePoint(k * lx, k * ly, visible);
    }

    /// <summary>
    /// Casts a ray from the satellite through the plane point and finds where it meets the globe.
    /// The hit is returned as a unit vector in local axes (X east, Y north, Z up), which is also the
    /// surface normal, so callers can light it directly.
    /// </summary>
    public bool TryHitLocal(double x, double y, out double hitX, out double hitY, out double hitZ)
    {
        hitX = hitY = hitZ = 0;
        if (!IsValid || !double.IsFinite(x) || !double.IsFinite(y))
            return false;

        // Camera at (0, 0, P); the image plane is z = 1; unit sphere at the origin.
        var dz = 1.0 - _p;
        var length = Math.Sqrt(x * x + y * y + dz * dz);
        var dxn = x / length;
        var dyn = y / length;
        var dzn = dz / length;

        var od = _p * dzn; // camera position dotted with the unit ray direction
        var discriminant = od * od - (_p * _p - 1.0);
        if (discriminant < 0)
            return false;

        var t = -od - Math.Sqrt(discriminant); // nearer of the two intersections
        hitX = t * dxn;
        hitY = t * dyn;
        hitZ = _p + t * dzn;
        return true;
    }

    /// <summary>Converts a unit vector in local axes to latitude and longitude, in degrees.</summary>
    public void LocalToGeo(double lx, double ly, double lz, out double latDeg, out double lonDeg)
    {
        var vx = lx * _eastX + ly * _northX + lz * _upX;
        var vy = lx * _eastY + ly * _northY + lz * _upY;
        var vz = ly * _northZ + lz * _upZ;

        latDeg = Math.Asin(Math.Clamp(vz, -1.0, 1.0)) * 180.0 / Math.PI;

        var lon = Math.Atan2(vy, vx) * 180.0 / Math.PI;
        lonDeg = lon <= -180.0 ? lon + 360.0 : lon;
    }

    public bool TryUnproject(double x, double y, out double latDeg, out double lonDeg)
    {
        latDeg = lonDeg = 0;
        if (!TryHitLocal(x, y, out var hx, out var hy, out var hz))
            return false;

        LocalToGeo(hx, hy, hz, out latDeg, out lonDeg);
        return true;
    }
}
