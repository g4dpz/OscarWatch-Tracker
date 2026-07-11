using OscarWatch.Core.Models;

namespace OscarWatch.Core.Tle;

public static class TleAltitude
{
    public static double ResolveAltitudeKm(double reportedAltitudeKm, SatelliteCatalogEntry satellite)
    {
        if (reportedAltitudeKm >= 100)
            return reportedAltitudeKm;

        var fromTle = EstimateFromMeanMotion(satellite);
        return fromTle > 0 ? fromTle : reportedAltitudeKm;
    }

    public static double EstimateFromMeanMotion(SatelliteCatalogEntry satellite)
    {
        if (!TleOrbitalSanity.TryReadLine2Elements(satellite.Line2, out _, out _, out var meanMotionRevPerDay)
            || meanMotionRevPerDay <= 0)
            return 0;

        var nRadPerSec = meanMotionRevPerDay * (2.0 * Math.PI / 86400.0);
        const double muKm3PerSec2 = 398600.4418;
        var semiMajorAxisKm = Math.Pow(muKm3PerSec2 / (nRadPerSec * nRadPerSec), 1.0 / 3.0);
        return Math.Max(0, semiMajorAxisKm - 6371.0);
    }
}
