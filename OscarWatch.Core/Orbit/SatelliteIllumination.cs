using OscarWatch.Core.Models;

namespace OscarWatch.Core.Orbit;

/// <summary>Earth cylindrical umbra test — sufficient for LEO battery/eclipse planning.</summary>
public static class SatelliteIllumination
{
    private const double EarthRadiusKm = 6371.0;

    public static bool IsInEclipse(EciPosition satelliteKm, EciPosition sunKm)
    {
        var sunDistanceKm = Math.Sqrt(
            sunKm.XKm * sunKm.XKm + sunKm.YKm * sunKm.YKm + sunKm.ZKm * sunKm.ZKm);
        if (sunDistanceKm <= 0)
            return false;

        var sunUnitX = sunKm.XKm / sunDistanceKm;
        var sunUnitY = sunKm.YKm / sunDistanceKm;
        var sunUnitZ = sunKm.ZKm / sunDistanceKm;

        var alongSunAxis =
            satelliteKm.XKm * sunUnitX
            + satelliteKm.YKm * sunUnitY
            + satelliteKm.ZKm * sunUnitZ;
        if (alongSunAxis >= 0)
            return false;

        var perpX = satelliteKm.XKm - alongSunAxis * sunUnitX;
        var perpY = satelliteKm.YKm - alongSunAxis * sunUnitY;
        var perpZ = satelliteKm.ZKm - alongSunAxis * sunUnitZ;
        var perpDistanceKm = Math.Sqrt(perpX * perpX + perpY * perpY + perpZ * perpZ);

        return perpDistanceKm < EarthRadiusKm;
    }

    public static bool IsSunlit(EciPosition satelliteKm, EciPosition sunKm) =>
        !IsInEclipse(satelliteKm, sunKm);
}
