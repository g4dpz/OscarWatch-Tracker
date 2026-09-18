// Feature: startup-io-rendering-optimisation, WorldMap render threshold

using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Controls;
using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;

namespace OscarWatch.Tests.Performance;

/// <summary>
/// World map invalidates only when a subpoint moves ≥ 1 px (or sat set changes),
/// and greyline time only when the UTC minute rolls.
/// </summary>
public class WorldMapThrottlePropertyTests
{
    private const double Width = 800.0;
    private const double Height = 400.0;
    private const double CentreLon = 0.0;

    private static SatelliteTrackState MakeState(string noradId, double latDeg, double lonDeg) => new()
    {
        Name = $"SAT-{noradId}",
        NoradId = noradId,
        Subpoint = new GeoCoordinate(latDeg, lonDeg),
    };

    [Property(MaxTest = 100)]
    public bool Returns_true_iff_any_subpoint_moved_at_least_1px(PositiveInt seedRaw)
    {
        var rng = new Random(seedRaw.Get);
        var count = rng.Next(1, 6);
        var newStates = new List<SatelliteTrackState>();
        var previousPositions = new Dictionary<string, (double X, double Y)>();

        for (var i = 0; i < count; i++)
        {
            var id = $"SAT{i:D5}";
            var oldLat = (rng.NextDouble() - 0.5) * 160.0;
            var oldLon = (rng.NextDouble() - 0.5) * 360.0;
            var (ox, oy) = EquirectangularProjection.GeoToPixel(oldLat, oldLon, Width, Height, CentreLon);
            previousPositions[id] = (ox, oy);

            var newLat = Math.Clamp(oldLat + (rng.NextDouble() - 0.5) * 2.0, -89.0, 89.0);
            var newLon = oldLon + (rng.NextDouble() - 0.5) * 4.0;
            newStates.Add(MakeState(id, newLat, newLon));
        }

        var expected = false;
        foreach (var state in newStates)
        {
            var (nx, ny) = EquirectangularProjection.GeoToPixel(
                state.Subpoint.LatitudeDeg, state.Subpoint.LongitudeDeg, Width, Height, CentreLon);
            var prev = previousPositions[state.NoradId];
            var dx = nx - prev.X;
            var dy = ny - prev.Y;
            if (dx * dx + dy * dy >= 1.0)
            {
                expected = true;
                break;
            }
        }

        var actual = WorldMapControl.HasMovedBeyondThreshold(
            newStates, previousPositions, Width, Height, CentreLon);
        return actual == expected;
    }

    [Property(MaxTest = 100)]
    public bool Returns_true_when_new_satellite_appears(PositiveInt seedRaw)
    {
        var rng = new Random(seedRaw.Get);
        var previousPositions = new Dictionary<string, (double X, double Y)>();
        var lat = (rng.NextDouble() - 0.5) * 160.0;
        var lon = (rng.NextDouble() - 0.5) * 360.0;
        previousPositions["SAT00001"] = EquirectangularProjection.GeoToPixel(lat, lon, Width, Height, CentreLon);

        var states = new List<SatelliteTrackState>
        {
            MakeState("SAT00001", lat, lon),
            MakeState("SAT00002", (rng.NextDouble() - 0.5) * 160.0, (rng.NextDouble() - 0.5) * 360.0),
        };

        return WorldMapControl.HasMovedBeyondThreshold(
            states, previousPositions, Width, Height, CentreLon);
    }

    [Property(MaxTest = 100)]
    public bool Returns_true_when_satellite_disappears(PositiveInt seedRaw)
    {
        var rng = new Random(seedRaw.Get);
        var previousPositions = new Dictionary<string, (double X, double Y)>
        {
            ["SAT00000"] = EquirectangularProjection.GeoToPixel(10, 20, Width, Height, CentreLon),
            ["SAT00001"] = EquirectangularProjection.GeoToPixel(-10, -20, Width, Height, CentreLon),
        };

        var lat = (rng.NextDouble() - 0.5) * 160.0;
        var lon = (rng.NextDouble() - 0.5) * 360.0;
        previousPositions["SAT00000"] = EquirectangularProjection.GeoToPixel(lat, lon, Width, Height, CentreLon);

        var states = new List<SatelliteTrackState> { MakeState("SAT00000", lat, lon) };
        return WorldMapControl.HasMovedBeyondThreshold(
            states, previousPositions, Width, Height, CentreLon);
    }

    [Property(MaxTest = 100)]
    public bool Returns_false_when_no_movement(PositiveInt seedRaw)
    {
        var rng = new Random(seedRaw.Get);
        var count = rng.Next(1, 6);
        var states = new List<SatelliteTrackState>();
        var previousPositions = new Dictionary<string, (double X, double Y)>();

        for (var i = 0; i < count; i++)
        {
            var id = $"SAT{i:D5}";
            var lat = (rng.NextDouble() - 0.5) * 160.0;
            var lon = (rng.NextDouble() - 0.5) * 360.0;
            states.Add(MakeState(id, lat, lon));
            previousPositions[id] = EquirectangularProjection.GeoToPixel(lat, lon, Width, Height, CentreLon);
        }

        return !WorldMapControl.HasMovedBeyondThreshold(
            states, previousPositions, Width, Height, CentreLon);
    }

    [Fact]
    public void Empty_states_empty_cache_returns_false()
    {
        Assert.False(WorldMapControl.HasMovedBeyondThreshold(
            [], new Dictionary<string, (double X, double Y)>(), Width, Height, CentreLon));
    }

    [Fact]
    public void Empty_states_nonempty_cache_returns_true()
    {
        Assert.True(WorldMapControl.HasMovedBeyondThreshold(
            [],
            new Dictionary<string, (double X, double Y)> { ["SAT00001"] = (100, 100) },
            Width,
            Height,
            CentreLon));
    }

    [Fact]
    public void FloorToUtcMinuteTicks_same_minute_equal()
    {
        var a = new DateTime(2026, 9, 8, 16, 5, 10, DateTimeKind.Utc);
        var b = new DateTime(2026, 9, 8, 16, 5, 59, DateTimeKind.Utc);
        Assert.Equal(WorldMapControl.FloorToUtcMinuteTicks(a), WorldMapControl.FloorToUtcMinuteTicks(b));
    }

    [Fact]
    public void FloorToUtcMinuteTicks_different_minute_not_equal()
    {
        var a = new DateTime(2026, 9, 8, 16, 5, 59, DateTimeKind.Utc);
        var b = new DateTime(2026, 9, 8, 16, 6, 0, DateTimeKind.Utc);
        Assert.NotEqual(WorldMapControl.FloorToUtcMinuteTicks(a), WorldMapControl.FloorToUtcMinuteTicks(b));
    }
}
