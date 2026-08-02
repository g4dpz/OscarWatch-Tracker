using OscarWatch.Core.Models;

namespace OscarWatch.Core.Display;

/// <summary>Samples a horizon mask into dense az/el points for polar plot drawing.</summary>
public static class HorizonMaskPolarGeometry
{
    public const int DefaultSampleCount = 72;

    /// <summary>
    /// Dense skyline samples (azimuth ascending, then wrap). Empty when mask has no points.
    /// </summary>
    public static IReadOnlyList<(double AzimuthDeg, double ElevationDeg)> SampleSkyline(
        HorizonMask? mask,
        int sampleCount = DefaultSampleCount)
    {
        if (mask is null || !mask.HasPoints || sampleCount < 3)
            return [];

        var list = new List<(double, double)>(sampleCount);
        for (var i = 0; i < sampleCount; i++)
        {
            var az = i * 360.0 / sampleCount;
            list.Add((az, mask.ElevationAt(az)));
        }

        return list;
    }

    public static bool IsObstructed(
        HorizonMask? mask,
        double azimuthDeg,
        double elevationDeg,
        double minimumElevationDeg)
    {
        var floor = mask?.EffectiveFloor(azimuthDeg, minimumElevationDeg) ?? minimumElevationDeg;
        return elevationDeg < floor;
    }
}
