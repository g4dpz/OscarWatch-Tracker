using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Orbit;

namespace OscarWatch.Tests;

public class SunlightSegmentPredictorTests
{
    private static SatelliteCatalogEntry IssEntry => new()
    {
        Name = "ISS (ZARYA)",
        NoradId = "25544",
        Line1 = "1 25544U 98067A   25205.51782528  .00016717  00000+0  10270-3 0  9993",
        Line2 = "2 25544  51.6416 247.4627 0006703 130.5360 325.0288 15.50415322908603"
    };

    [Fact]
    public async Task Predict_produces_alternating_segments_for_leo()
    {
        var predictor = new SunlightSegmentPredictor(new NullOrbitPropagator());
        var utcStart = new DateTime(2026, 5, 23, 12, 0, 0, DateTimeKind.Utc);
        var segments = await predictor.PredictAsync(
            IssEntry,
            utcStart,
            TimeSpan.FromHours(6));

        Assert.NotEmpty(segments);
        Assert.All(segments, s => Assert.True(s.EndUtc > s.StartUtc));
        Assert.Contains(segments, s => s.IsSunlit);
        Assert.Contains(segments, s => !s.IsSunlit);
    }

    [Fact]
    public async Task Predict_segments_are_contiguous_over_range()
    {
        var predictor = new SunlightSegmentPredictor(new NullOrbitPropagator());
        var utcStart = new DateTime(2026, 5, 23, 0, 0, 0, DateTimeKind.Utc);
        var duration = TimeSpan.FromDays(2);
        var segments = await predictor.PredictAsync(IssEntry, utcStart, duration);

        Assert.NotEmpty(segments);
        Assert.Equal(utcStart, segments[0].StartUtc, TimeSpan.FromSeconds(2));
        Assert.Equal(utcStart + duration, segments[^1].EndUtc, TimeSpan.FromSeconds(2));

        for (var i = 1; i < segments.Count; i++)
            Assert.Equal(segments[i - 1].EndUtc, segments[i].StartUtc, TimeSpan.FromMilliseconds(500));
    }

    private sealed class NullOrbitPropagator : IOrbitPropagator
    {
        public IReadOnlyCollection<string> LoadedNoradIds { get; } = [];
        public void Clear() { }
        public void LoadSatellite(SatelliteCatalogEntry entry) { }
        public void RemoveSatellite(string noradId) { }
        public bool HasSatellite(string noradId) => false;
        public GeoCoordinate GetSubpoint(string noradId, DateTime utc) => new(0, 0, 0);
        public EciPosition GetEciPosition(string noradId, DateTime utc) => new(0, 0, 0);
        public LookAngles GetLookAngles(string noradId, GroundStation site, DateTime utc) =>
            new(0, 0, 0, 0);
    }
}
