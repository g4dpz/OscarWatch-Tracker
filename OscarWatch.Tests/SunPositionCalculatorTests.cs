using OscarWatch.Core.Orbit;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 8.1, 8.3**
///
/// Example-based tests verifying that <see cref="SunPositionCalculator"/>
/// returns positions consistent with published solar ephemeris at known
/// reference epochs (J2000.0 and March equinox 2024).
/// </summary>
public sealed class SunPositionCalculatorTests
{
    private const double AuKm = 149_597_870.7;

    /// <summary>
    /// J2000.0 epoch (2000-01-01T12:00:00Z) returns a distance within 2% of 1 AU.
    /// 2% tolerance: [146,605,913 km, 152,589,828 km].
    /// </summary>
    [Fact]
    public void J2000_epoch_returns_distance_within_2_percent_of_1_au()
    {
        var j2000 = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var position = SunPositionCalculator.GetPosition(j2000);

        var distance = Math.Sqrt(
            position.XKm * position.XKm +
            position.YKm * position.YKm +
            position.ZKm * position.ZKm);

        var lowerBound = AuKm * 0.98; // 146,605,913 km
        var upperBound = AuKm * 1.02; // 152,589,828 km

        Assert.InRange(distance, lowerBound, upperBound);
    }

    /// <summary>
    /// March equinox 2024 (~2024-03-20T03:06:00Z) returns Z component within 10,000 km of zero.
    /// At the equinox the sun's declination is approximately zero, so the Z component
    /// (which is proportional to sin(declination)) should be near zero. The low-precision
    /// algorithm achieves ~0.003° declination accuracy, which at 1 AU translates to ~8500 km.
    /// A 10,000 km tolerance validates the equinox behaviour while accommodating the
    /// algorithm's precision limits.
    /// </summary>
    [Fact]
    public void March_equinox_2024_returns_z_component_near_zero()
    {
        var equinox2024 = new DateTime(2024, 3, 20, 3, 6, 0, DateTimeKind.Utc);

        var position = SunPositionCalculator.GetPosition(equinox2024);

        // At equinox, Z should be close to zero relative to the full sun distance (~149.6M km).
        // The low-precision model achieves ~0.003° declination error at equinox, which at
        // 1 AU distance produces a Z of ~8500 km. We use 10,000 km as a generous tolerance
        // that still confirms the equinox Z is negligible relative to the total distance.
        Assert.InRange(position.ZKm, -10_000.0, 10_000.0);
    }
}
