// Feature: test-coverage-expansion, Property 21: Sun Position Distance Range

using FsCheck.Xunit;
using OscarWatch.Core.Orbit;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 8.2, 8.4**
///
/// Property-based tests verifying that <see cref="SunPositionCalculator"/>
/// returns positions with magnitudes consistent with the Earth-Sun distance
/// range for any UTC timestamp between 2000 and 2035.
/// </summary>
public class SunPositionCalculatorPropertyTests
{
    private static readonly DateTime BaseDate = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // ~13,000 days covers 2000-01-01 to approximately 2035-08-14
    private const int MaxDayOffset = 13_000;

    /// <summary>
    /// Property 21: Sun Position Distance Range.
    ///
    /// For any UTC timestamp between 2000-01-01 and 2035-12-31, the magnitude
    /// of the vector returned by GetPosition is in [147,000,000 km, 153,000,000 km].
    /// </summary>
    [Property]
    public bool Distance_magnitude_is_within_earth_sun_range(int rawOffset)
    {
        // Constrain offset to [0, MaxDayOffset] days from base date
        var dayOffset = ((rawOffset % MaxDayOffset) + MaxDayOffset) % MaxDayOffset;
        var utc = BaseDate.AddDays(dayOffset);

        var position = SunPositionCalculator.GetPosition(utc);

        var magnitude = Math.Sqrt(
            position.XKm * position.XKm +
            position.YKm * position.YKm +
            position.ZKm * position.ZKm);

        return magnitude >= 147_000_000.0 && magnitude <= 153_000_000.0;
    }
}
