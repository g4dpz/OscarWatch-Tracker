namespace OscarWatch.Core.Orbit;

/// <summary>Shared astronomical utility methods used across OscarWatch.Core.</summary>
internal static class AstronomyMath
{
    /// <summary>Converts a UTC <see cref="DateTime"/> to a Julian Date (JD).</summary>
    public static double ToJulianDate(DateTime utc)
    {
        var year = utc.Year;
        var month = utc.Month;
        if (month <= 2)
        {
            year--;
            month += 12;
        }

        var century = year / 100;
        var b = 2 - century + century / 4;
        return Math.Floor(365.25 * (year + 4716))
            + Math.Floor(30.6001 * (month + 1))
            + utc.Day
            + utc.TimeOfDay.TotalDays
            + b
            - 1524.5;
    }
}
