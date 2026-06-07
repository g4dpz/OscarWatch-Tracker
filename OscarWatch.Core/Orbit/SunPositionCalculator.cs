using OscarWatch.Core.Models;

namespace OscarWatch.Core.Orbit;

/// <summary>Low-precision sun position in Earth-centred inertial coordinates (km).</summary>
public static class SunPositionCalculator
{
    private const double AuKm = 149_597_870.7;

    public static EciPosition GetPosition(DateTime utc)
    {
        var jd = AstronomyMath.ToJulianDate(utc);
        var t = (jd - 2451545.0) / 36525.0;

        var meanLongitudeDeg = Normalize360(280.46646 + 36000.76983 * t + 0.0003032 * t * t);
        var meanAnomalyDeg = Normalize360(357.52911 + 35999.05029 * t - 0.0001537 * t * t);
        var meanAnomalyRad = meanAnomalyDeg * Math.PI / 180.0;

        var eccentricity = 0.016708634 - 0.000042037 * t - 0.0000001267 * t * t;
        var equationOfCenterDeg =
            (1.914602 - 0.004817 * t - 0.000014 * t * t) * Math.Sin(meanAnomalyRad)
            + (0.019993 - 0.000101 * t) * Math.Sin(2 * meanAnomalyRad)
            + 0.000289 * Math.Sin(3 * meanAnomalyRad);

        var sunEclipticLongitudeRad = (meanLongitudeDeg + equationOfCenterDeg) * Math.PI / 180.0;
        var obliquityRad = (23.439291 - 0.0130042 * t) * Math.PI / 180.0;

        var distanceKm = AuKm * (1.000001018 * (1 - eccentricity * eccentricity))
            / (1 + eccentricity * Math.Cos(meanAnomalyRad + equationOfCenterDeg * Math.PI / 180.0));

        var rightAscensionRad = Math.Atan2(
            Math.Cos(obliquityRad) * Math.Sin(sunEclipticLongitudeRad),
            Math.Cos(sunEclipticLongitudeRad));
        var declinationRad = Math.Asin(Math.Sin(obliquityRad) * Math.Sin(sunEclipticLongitudeRad));

        var cosDec = Math.Cos(declinationRad);
        return new EciPosition(
            distanceKm * cosDec * Math.Cos(rightAscensionRad),
            distanceKm * cosDec * Math.Sin(rightAscensionRad),
            distanceKm * Math.Sin(declinationRad));
    }

    private static double Normalize360(double degrees)
    {
        var normalized = degrees % 360.0;
        return normalized < 0 ? normalized + 360.0 : normalized;
    }
}
