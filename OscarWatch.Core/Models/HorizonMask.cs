namespace OscarWatch.Core.Models;

/// <summary>
/// Per-station azimuth→elevation skyline. Empty points means unused (scalar min elevation only).
/// </summary>
public sealed class HorizonMask
{
    public List<HorizonMaskPoint> Points { get; set; } = [];

    public bool HasPoints => Points.Count > 0;

    /// <summary>Obstruction elevation at <paramref name="azimuthDeg"/>; empty mask returns 0.</summary>
    public double ElevationAt(double azimuthDeg)
    {
        if (Points.Count == 0)
            return 0;

        var az = NormalizeAzimuth(azimuthDeg);
        var sorted = GetSortedClampedPoints();
        if (sorted.Count == 0)
            return 0;
        if (sorted.Count == 1)
            return sorted[0].ElevationDeg;

        // Find segment that contains az, wrapping across 0°/360°.
        for (var i = 0; i < sorted.Count; i++)
        {
            var a = sorted[i];
            var b = sorted[(i + 1) % sorted.Count];
            var aAz = a.AzimuthDeg;
            var bAz = b.AzimuthDeg;

            bool inSegment;
            double span;
            double t;
            if (i < sorted.Count - 1)
            {
                inSegment = az >= aAz && az <= bAz;
                span = bAz - aAz;
                t = span <= 1e-9 ? 0 : (az - aAz) / span;
            }
            else
            {
                // Wrap: from last point to first + 360°.
                inSegment = az >= aAz || az <= bAz;
                span = (bAz + 360.0) - aAz;
                var azAdj = az >= aAz ? az : az + 360.0;
                t = span <= 1e-9 ? 0 : (azAdj - aAz) / span;
            }

            if (!inSegment)
                continue;

            t = Math.Clamp(t, 0, 1);
            return a.ElevationDeg + t * (b.ElevationDeg - a.ElevationDeg);
        }

        return sorted[^1].ElevationDeg;
    }

    public double EffectiveFloor(double azimuthDeg, double minimumElevationDeg) =>
        Math.Max(minimumElevationDeg, ElevationAt(azimuthDeg));

    /// <summary>Sort by azimuth, clamp ranges, merge duplicate azimuths (keep last).</summary>
    public void Normalize()
    {
        Points = GetSortedClampedPoints();
    }

    public HorizonMask Clone()
    {
        var copy = new HorizonMask();
        foreach (var p in Points)
            copy.Points.Add(new HorizonMaskPoint(p.AzimuthDeg, p.ElevationDeg));
        return copy;
    }

    private List<HorizonMaskPoint> GetSortedClampedPoints()
    {
        var map = new SortedDictionary<double, double>();
        foreach (var p in Points)
        {
            var az = NormalizeAzimuth(p.AzimuthDeg);
            var el = Math.Clamp(p.ElevationDeg, 0, 90);
            map[az] = el;
        }

        return map.Select(kv => new HorizonMaskPoint(kv.Key, kv.Value)).ToList();
    }

    private static double NormalizeAzimuth(double azimuthDeg)
    {
        var az = azimuthDeg % 360.0;
        if (az < 0)
            az += 360.0;
        // Treat 360 as 0 for stable keys.
        if (az >= 360.0 - 1e-9)
            az = 0;
        return az;
    }
}
