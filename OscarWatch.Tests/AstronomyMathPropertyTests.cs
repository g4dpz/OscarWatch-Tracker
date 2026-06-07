// Feature: performance-optimisations, Property 10: ToJulianDate round-trip preserves date to within 1 second

using FsCheck.Xunit;
using OscarWatch.Core.Orbit;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 10.4, 10.5**
/// </summary>
public class AstronomyMathPropertyTests
{
    private static readonly long MinTicks = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
    private static readonly long MaxTicks = new DateTime(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
    private static readonly long TickRange = MaxTicks - MinTicks;

    /// <summary>
    /// Property 10: ToJulianDate round-trip preserves date to within 1 second.
    /// Generates arbitrary DateTime values in [1900-01-01, 2100-01-01] UTC,
    /// converts to Julian Date using AstronomyMath.ToJulianDate,
    /// converts back to DateTime using the standard JD→Calendar formula,
    /// and asserts the round-trip result is within 1 second of the original.
    /// </summary>
    [Property]
    public bool ToJulianDate_RoundTrip_PreservesDateWithin1Second(long seed)
    {
        // Clamp the arbitrary long into [1900-01-01, 2100-01-01] UTC tick range
        var ticks = MinTicks + (Math.Abs(seed % TickRange));
        var original = new DateTime(ticks, DateTimeKind.Utc);

        var jd = AstronomyMath.ToJulianDate(original);
        var roundTripped = JdToDateTime(jd);

        var diff = Math.Abs((original - roundTripped).TotalSeconds);
        return diff <= 1.0;
    }

    private static DateTime JdToDateTime(double jd)
    {
        var z = (int)(jd + 0.5);
        var f = jd + 0.5 - z;
        int a;
        if (z < 2299161)
            a = z;
        else
        {
            var alpha = (int)((z - 1867216.25) / 36524.25);
            a = z + 1 + alpha - alpha / 4;
        }
        var b2 = a + 1524;
        var c = (int)((b2 - 122.1) / 365.25);
        var d = (int)(365.25 * c);
        var e = (int)((b2 - d) / 30.6001);
        var day = b2 - d - (int)(30.6001 * e);
        var month = e < 14 ? e - 1 : e - 13;
        var year = month > 2 ? c - 4716 : c - 4715;
        var fractionalDay = f;
        var totalSeconds = fractionalDay * 86400.0;
        var hours = (int)(totalSeconds / 3600);
        totalSeconds -= hours * 3600;
        var minutes = (int)(totalSeconds / 60);
        totalSeconds -= minutes * 60;
        return new DateTime(year, month, day, hours, minutes, (int)totalSeconds, DateTimeKind.Utc);
    }
}
