using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Core.Rotator;

namespace OscarWatch.Tests;

/// <summary>
/// Unit tests for <see cref="PassProfileBuilder"/> using a mock propagator.
/// Validates: Requirements 1.3
/// </summary>
public class PassProfileBuilderUnitTests
{
    private static readonly GroundStation TestStation = new()
    {
        DisplayName = "TestSite",
        LatitudeDeg = 51.5,
        LongitudeDeg = -0.1,
        AltitudeMetersAsl = 50
    };

    private static PassInfo CreatePass(DateTime aos, DateTime los) => new()
    {
        SatelliteName = "TEST-SAT",
        NoradId = "99999",
        AosUtc = aos,
        LosUtc = los,
        MaxElevationDeg = 45.0,
        MaxElevationUtc = aos.AddSeconds((los - aos).TotalSeconds / 2),
        AosAzimuthDeg = 180.0,
        LosAzimuthDeg = 0.0
    };

    #region Correct number of points

    [Fact]
    public void Build_returns_correct_number_of_points_for_10_second_pass()
    {
        // A 10-second pass should produce 11 points (AOS + 10 intervals = 11 inclusive)
        var aos = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var los = aos.AddSeconds(10);
        var pass = CreatePass(aos, los);

        var propagator = new MockOrbitPropagator(
            (_, _, utc) => new LookAngles(180.0, 45.0, 500.0));

        var profile = PassProfileBuilder.Build(pass, pass.NoradId, TestStation, propagator);

        Assert.NotNull(profile);
        Assert.Equal(11, profile.Points.Count);
    }

    [Fact]
    public void Build_returns_correct_number_of_points_for_600_second_pass()
    {
        // A 10-minute (600 second) pass should produce 601 points
        var aos = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var los = aos.AddSeconds(600);
        var pass = CreatePass(aos, los);

        var propagator = new MockOrbitPropagator(
            (_, _, utc) => new LookAngles(90.0, 30.0, 400.0));

        var profile = PassProfileBuilder.Build(pass, pass.NoradId, TestStation, propagator);

        Assert.NotNull(profile);
        Assert.Equal(601, profile.Points.Count);
    }

    [Fact]
    public void Build_points_have_correct_timestamps()
    {
        var aos = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var los = aos.AddSeconds(5);
        var pass = CreatePass(aos, los);

        var propagator = new MockOrbitPropagator(
            (_, _, utc) => new LookAngles(100.0, 20.0, 300.0));

        var profile = PassProfileBuilder.Build(pass, pass.NoradId, TestStation, propagator);

        Assert.NotNull(profile);
        for (var i = 0; i < profile.Points.Count; i++)
        {
            Assert.Equal(aos.AddSeconds(i), profile.Points[i].Utc);
        }
    }

    [Fact]
    public void Build_points_contain_propagator_azimuth_and_elevation()
    {
        var aos = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var los = aos.AddSeconds(3);
        var pass = CreatePass(aos, los);

        // Return azimuth that increases by 10° per second
        var propagator = new MockOrbitPropagator(
            (_, _, utc) =>
            {
                var secondsFromAos = (utc - aos).TotalSeconds;
                return new LookAngles(100.0 + secondsFromAos * 10.0, 20.0 + secondsFromAos * 5.0, 400.0);
            });

        var profile = PassProfileBuilder.Build(pass, pass.NoradId, TestStation, propagator);

        Assert.NotNull(profile);
        Assert.Equal(100.0, profile.Points[0].AzimuthDeg);
        Assert.Equal(20.0, profile.Points[0].ElevationDeg);
        Assert.Equal(110.0, profile.Points[1].AzimuthDeg);
        Assert.Equal(25.0, profile.Points[1].ElevationDeg);
        Assert.Equal(120.0, profile.Points[2].AzimuthDeg);
        Assert.Equal(30.0, profile.Points[2].ElevationDeg);
    }

    #endregion

    #region Error handling — propagator throws for some points

    [Fact]
    public void Build_skips_failed_points_when_propagator_throws_for_some()
    {
        var aos = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var los = aos.AddSeconds(10);
        var pass = CreatePass(aos, los);

        // Throw on indices 3 and 7 (2 out of 11 points fail — well below 50%)
        var failIndices = new HashSet<int> { 3, 7 };
        var callIndex = 0;

        var propagator = new MockOrbitPropagator(
            (_, _, utc) =>
            {
                var idx = callIndex++;
                if (failIndices.Contains(idx))
                    throw new InvalidOperationException("Propagation failed");
                return new LookAngles(180.0, 45.0, 500.0);
            });

        var profile = PassProfileBuilder.Build(pass, pass.NoradId, TestStation, propagator);

        Assert.NotNull(profile);
        // 11 expected points - 2 failures = 9 successful points
        Assert.Equal(9, profile.Points.Count);
    }

    [Fact]
    public void Build_returns_profile_when_fewer_than_50_percent_fail()
    {
        var aos = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var los = aos.AddSeconds(10);
        var pass = CreatePass(aos, los);

        // 11 expected points — fail 5 (45% failure, just under 50%)
        var failIndices = new HashSet<int> { 0, 2, 4, 6, 8 };
        var callIndex = 0;

        var propagator = new MockOrbitPropagator(
            (_, _, utc) =>
            {
                var idx = callIndex++;
                if (failIndices.Contains(idx))
                    throw new InvalidOperationException("Propagation failed");
                return new LookAngles(90.0, 30.0, 400.0);
            });

        var profile = PassProfileBuilder.Build(pass, pass.NoradId, TestStation, propagator);

        Assert.NotNull(profile);
        Assert.Equal(6, profile.Points.Count);
    }

    #endregion

    #region Null return when >50% of points fail

    [Fact]
    public void Build_returns_null_when_more_than_50_percent_of_points_fail()
    {
        var aos = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var los = aos.AddSeconds(10);
        var pass = CreatePass(aos, los);

        // 11 expected points — fail 6 (>50%)
        var failIndices = new HashSet<int> { 0, 1, 2, 3, 4, 5 };
        var callIndex = 0;

        var propagator = new MockOrbitPropagator(
            (_, _, utc) =>
            {
                var idx = callIndex++;
                if (failIndices.Contains(idx))
                    throw new InvalidOperationException("Propagation failed");
                return new LookAngles(90.0, 30.0, 400.0);
            });

        var profile = PassProfileBuilder.Build(pass, pass.NoradId, TestStation, propagator);

        Assert.Null(profile);
    }

    [Fact]
    public void Build_returns_null_when_all_points_fail()
    {
        var aos = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var los = aos.AddSeconds(5);
        var pass = CreatePass(aos, los);

        var propagator = new MockOrbitPropagator(
            (_, _, _) => throw new InvalidOperationException("All propagation failed"));

        var profile = PassProfileBuilder.Build(pass, pass.NoradId, TestStation, propagator);

        Assert.Null(profile);
    }

    [Fact]
    public void Build_returns_null_when_exactly_half_plus_one_fail()
    {
        // 11 expected points, threshold is >50% = >5.5, so 6 failures should trigger null
        var aos = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var los = aos.AddSeconds(10);
        var pass = CreatePass(aos, los);

        // Fail indices 0-5 (6 out of 11 = 54.5% > 50%)
        var callIndex = 0;
        var propagator = new MockOrbitPropagator(
            (_, _, _) =>
            {
                var idx = callIndex++;
                if (idx < 6)
                    throw new InvalidOperationException("Propagation failed");
                return new LookAngles(90.0, 30.0, 400.0);
            });

        var profile = PassProfileBuilder.Build(pass, pass.NoradId, TestStation, propagator);

        Assert.Null(profile);
    }

    #endregion

    #region Argument validation

    [Fact]
    public void Build_throws_ArgumentNullException_for_null_pass()
    {
        var propagator = new MockOrbitPropagator((_, _, _) => new LookAngles(0, 0, 0));

        Assert.Throws<ArgumentNullException>(
            () => PassProfileBuilder.Build(null!, "12345", TestStation, propagator));
    }

    [Fact]
    public void Build_throws_ArgumentNullException_for_null_noradId()
    {
        var aos = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var pass = CreatePass(aos, aos.AddSeconds(10));
        var propagator = new MockOrbitPropagator((_, _, _) => new LookAngles(0, 0, 0));

        Assert.Throws<ArgumentNullException>(
            () => PassProfileBuilder.Build(pass, null!, TestStation, propagator));
    }

    [Fact]
    public void Build_throws_ArgumentNullException_for_null_site()
    {
        var aos = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var pass = CreatePass(aos, aos.AddSeconds(10));
        var propagator = new MockOrbitPropagator((_, _, _) => new LookAngles(0, 0, 0));

        Assert.Throws<ArgumentNullException>(
            () => PassProfileBuilder.Build(pass, "12345", null!, propagator));
    }

    [Fact]
    public void Build_throws_ArgumentNullException_for_null_propagator()
    {
        var aos = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var pass = CreatePass(aos, aos.AddSeconds(10));

        Assert.Throws<ArgumentNullException>(
            () => PassProfileBuilder.Build(pass, "12345", TestStation, null!));
    }

    #endregion

    #region Mock propagator

    /// <summary>
    /// Simple mock implementation of <see cref="IOrbitPropagator"/> that delegates
    /// <see cref="GetLookAngles"/> to a configurable function, allowing tests to
    /// control return values and inject exceptions.
    /// </summary>
    private sealed class MockOrbitPropagator : IOrbitPropagator
    {
        private readonly Func<string, GroundStation, DateTime, LookAngles> _getLookAngles;

        public MockOrbitPropagator(Func<string, GroundStation, DateTime, LookAngles> getLookAngles)
        {
            _getLookAngles = getLookAngles;
        }

        public IReadOnlyCollection<string> LoadedNoradIds => Array.Empty<string>();

        public void LoadSatellite(SatelliteCatalogEntry entry) { }
        public void RemoveSatellite(string noradId) { }
        public void Clear() { }
        public bool HasSatellite(string noradId) => true;

        public LookAngles GetLookAngles(string noradId, GroundStation site, DateTime utc)
            => _getLookAngles(noradId, site, utc);

        public GeoCoordinate GetSubpoint(string noradId, DateTime utc)
            => new(0, 0, 0);

        public EciPosition GetEciPosition(string noradId, DateTime utc)
            => new(0, 0, 0);
    }

    #endregion
}
