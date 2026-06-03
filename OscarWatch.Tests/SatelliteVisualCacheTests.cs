using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

public sealed class SatelliteVisualCacheTests
{
    [Fact]
    public void IsStale_when_requested_utc_is_before_cached_utc()
    {
        var cached = new DateTime(2026, 6, 3, 12, 10, 0, DateTimeKind.Utc);
        var requested = cached.AddMinutes(-5);

        Assert.True(SatelliteVisualCache.IsStale(requested, cached, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void IsStale_when_requested_utc_is_after_cached_utc_beyond_interval()
    {
        var cached = new DateTime(2026, 6, 3, 12, 0, 0, DateTimeKind.Utc);
        var requested = cached.AddSeconds(5);

        Assert.True(SatelliteVisualCache.IsStale(requested, cached, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void IsFresh_when_requested_utc_is_within_interval_of_cached_utc()
    {
        var cached = new DateTime(2026, 6, 3, 12, 0, 0, DateTimeKind.Utc);
        var requested = cached.AddMilliseconds(500);

        Assert.False(SatelliteVisualCache.IsStale(requested, cached, TimeSpan.FromSeconds(1)));
    }
}
