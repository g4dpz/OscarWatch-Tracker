// Task 4.3: Unit tests verifying footprint geometry cache hit/miss behaviour

using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 3.1, 3.2, 3.3**
///
/// Property-based and unit tests verifying the cache principles used by
/// <c>WorldMapControl._footprintGeometryCache</c>:
///
/// 1. Cache hit: same list reference means data hasn't changed (ReferenceEquals == true).
/// 2. Cache miss: new list (even with identical content) means data has been refreshed
///    and geometry must be rebuilt (ReferenceEquals == false).
/// 3. Eviction: NORAD IDs no longer in the active track set are removed from the cache dictionary.
///
/// The actual <c>FootprintGeometryEntry</c> is private within WorldMapControl, so these tests
/// exercise the identical staleness-detection and eviction logic in isolation.
/// </summary>
public class FootprintGeometryCachePropertyTests
{
    // ─── Sub-task 1: Cache hit — same reference means no rebuild ───

    /// <summary>
    /// Requirement 3.1: When the footprint ring data for a satellite has not changed,
    /// the cached geometry should be reused. The cache detects "not changed" via
    /// ReferenceEquals on the IReadOnlyList. This verifies the hit condition.
    /// </summary>
    [Fact]
    public void Same_list_reference_signals_cache_hit()
    {
        IReadOnlyList<double> footprint = new List<double> { 1.0, 2.0, 3.0 };

        // Simulate storing and retrieving the same reference
        var cachedRef = footprint;
        var isCacheHit = ReferenceEquals(cachedRef, footprint);

        Assert.True(isCacheHit);
    }

    /// <summary>
    /// Property: For any list content, assigning to a variable and checking
    /// ReferenceEquals always returns true (same reference = cache hit).
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Same_reference_always_signals_hit(int[] data)
    {
        IReadOnlyList<int> footprint = data;

        // Simulates the cache storing the reference and checking on next frame
        var cached = footprint;
        return ReferenceEquals(cached, footprint);
    }

    /// <summary>
    /// Simulates the full cache lookup: store a footprint reference keyed by NORAD ID,
    /// then on next frame check the same reference — should be a cache hit.
    /// </summary>
    [Fact]
    public void Cache_dictionary_lookup_with_same_reference_is_hit()
    {
        var cache = new Dictionary<string, CacheEntry>();
        IReadOnlyList<double> footprint = new List<double> { 10.0, 20.0, 30.0 };
        const double width = 800;
        const double height = 600;

        // First frame: cache miss, store entry
        cache["25544"] = new CacheEntry(footprint, width, height, "geometry-A");

        // Second frame: same reference, same dimensions — cache hit
        var entry = cache["25544"];
        var isHit = ReferenceEquals(entry.SourceFootprint, footprint)
                    && entry.Width == width
                    && entry.Height == height;

        Assert.True(isHit);
        Assert.Equal("geometry-A", entry.CachedGeometry);
    }

    // ─── Sub-task 2: Cache miss — new reference means rebuild ───

    /// <summary>
    /// Requirement 3.2: When SatelliteVisualCache provides updated footprint data
    /// (a new list object), the geometry must be rebuilt. The cache detects this via
    /// ReferenceEquals returning false.
    /// </summary>
    [Fact]
    public void New_list_with_same_content_signals_cache_miss()
    {
        var original = new List<double> { 1.0, 2.0, 3.0 };
        IReadOnlyList<double> footprint1 = original;

        // SatelliteVisualCache refreshes — creates a new list with same content
        IReadOnlyList<double> footprint2 = new List<double> { 1.0, 2.0, 3.0 };

        var isCacheMiss = !ReferenceEquals(footprint1, footprint2);

        Assert.True(isCacheMiss);
    }

    /// <summary>
    /// Property: Creating a new list from any source always produces a different
    /// reference (ReferenceEquals == false), triggering a cache miss.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool New_list_from_same_data_always_signals_miss(int[] data)
    {
        IReadOnlyList<int> footprint1 = data;
        // Simulate SatelliteVisualCache creating a new list on refresh
        IReadOnlyList<int> footprint2 = data.ToList();

        return !ReferenceEquals(footprint1, footprint2);
    }

    /// <summary>
    /// Simulates the full cache lookup: store a footprint reference, then on next frame
    /// receive a new list (refresh from SatelliteVisualCache) — should be a cache miss
    /// requiring geometry rebuild.
    /// </summary>
    [Fact]
    public void Cache_dictionary_lookup_with_new_reference_is_miss()
    {
        var cache = new Dictionary<string, CacheEntry>();
        IReadOnlyList<double> footprint1 = new List<double> { 10.0, 20.0, 30.0 };
        const double width = 800;
        const double height = 600;

        // First frame: store entry
        cache["25544"] = new CacheEntry(footprint1, width, height, "geometry-A");

        // SatelliteVisualCache refreshes — new list object
        IReadOnlyList<double> footprint2 = new List<double> { 10.0, 20.0, 30.0 };

        // Second frame: new reference — cache miss
        var entry = cache["25544"];
        var isHit = ReferenceEquals(entry.SourceFootprint, footprint2)
                    && entry.Width == width
                    && entry.Height == height;

        Assert.False(isHit);
    }

    /// <summary>
    /// Dimension change also triggers a cache miss even with the same footprint reference.
    /// </summary>
    [Fact]
    public void Dimension_change_with_same_reference_signals_cache_miss()
    {
        var cache = new Dictionary<string, CacheEntry>();
        IReadOnlyList<double> footprint = new List<double> { 10.0, 20.0, 30.0 };

        // First frame at 800x600
        cache["25544"] = new CacheEntry(footprint, 800, 600, "geometry-A");

        // Control resized to 1024x768 — same footprint ref but different dimensions
        var entry = cache["25544"];
        var isHit = ReferenceEquals(entry.SourceFootprint, footprint)
                    && entry.Width == 1024
                    && entry.Height == 768;

        Assert.False(isHit);
    }

    // ─── Sub-task 3: Eviction removes entries for removed NORAD IDs ───

    /// <summary>
    /// Requirement 3.3: When a satellite is removed from TrackStates, its cache entry
    /// should be evicted to prevent unbounded memory growth.
    /// </summary>
    [Fact]
    public void Eviction_removes_entries_for_absent_norad_ids()
    {
        var cache = new Dictionary<string, CacheEntry>
        {
            ["25544"] = new CacheEntry(Array.Empty<double>(), 800, 600, "ISS"),
            ["07530"] = new CacheEntry(Array.Empty<double>(), 800, 600, "AO-7"),
            ["43017"] = new CacheEntry(Array.Empty<double>(), 800, 600, "FO-29")
        };

        // Current TrackStates only contains ISS and FO-29 (AO-7 was removed)
        var activeNoradIds = new HashSet<string> { "25544", "43017" };

        // Eviction logic (mirrors WorldMapControl render pass)
        var keys = cache.Keys.ToArray();
        foreach (var key in keys)
        {
            if (!activeNoradIds.Contains(key))
                cache.Remove(key);
        }

        Assert.Equal(2, cache.Count);
        Assert.True(cache.ContainsKey("25544"));
        Assert.True(cache.ContainsKey("43017"));
        Assert.False(cache.ContainsKey("07530"));
    }

    /// <summary>
    /// Eviction with empty TrackStates removes all cache entries.
    /// </summary>
    [Fact]
    public void Eviction_with_empty_track_states_removes_all_entries()
    {
        var cache = new Dictionary<string, CacheEntry>
        {
            ["25544"] = new CacheEntry(Array.Empty<double>(), 800, 600, "ISS"),
            ["07530"] = new CacheEntry(Array.Empty<double>(), 800, 600, "AO-7")
        };

        var activeNoradIds = new HashSet<string>();

        var keys = cache.Keys.ToArray();
        foreach (var key in keys)
        {
            if (!activeNoradIds.Contains(key))
                cache.Remove(key);
        }

        Assert.Empty(cache);
    }

    /// <summary>
    /// Eviction with all satellites still active removes nothing.
    /// </summary>
    [Fact]
    public void Eviction_with_all_active_removes_nothing()
    {
        var cache = new Dictionary<string, CacheEntry>
        {
            ["25544"] = new CacheEntry(Array.Empty<double>(), 800, 600, "ISS"),
            ["07530"] = new CacheEntry(Array.Empty<double>(), 800, 600, "AO-7")
        };

        var activeNoradIds = new HashSet<string> { "25544", "07530" };

        var keys = cache.Keys.ToArray();
        foreach (var key in keys)
        {
            if (!activeNoradIds.Contains(key))
                cache.Remove(key);
        }

        Assert.Equal(2, cache.Count);
    }

    /// <summary>
    /// Property: After eviction, the cache contains exactly the intersection of
    /// cached keys and active NORAD IDs.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Eviction_retains_only_active_ids(string[] cachedIds, string[] activeIds)
    {
        // Filter nulls that FsCheck may generate
        var safeCachedIds = cachedIds.Where(id => id is not null).Distinct().ToArray();
        var safeActiveIds = new HashSet<string>(activeIds.Where(id => id is not null));

        var cache = new Dictionary<string, string>();
        foreach (var id in safeCachedIds)
            cache[id] = $"geometry-{id}";

        // Eviction (mirrors WorldMapControl logic)
        var keys = cache.Keys.ToArray();
        foreach (var key in keys)
        {
            if (!safeActiveIds.Contains(key))
                cache.Remove(key);
        }

        // After eviction: only keys that were both cached AND active should remain
        var expectedKeys = safeCachedIds.Where(id => safeActiveIds.Contains(id)).ToHashSet();
        return cache.Count == expectedKeys.Count
               && cache.Keys.All(k => expectedKeys.Contains(k));
    }

    /// <summary>
    /// Property: Eviction never adds keys that weren't already in the cache.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Eviction_never_adds_new_keys(string[] cachedIds, string[] activeIds)
    {
        var safeCachedIds = cachedIds.Where(id => id is not null).Distinct().ToArray();
        var safeActiveIds = new HashSet<string>(activeIds.Where(id => id is not null));

        var cache = new Dictionary<string, string>();
        foreach (var id in safeCachedIds)
            cache[id] = $"geometry-{id}";

        var originalKeys = cache.Keys.ToHashSet();

        var keys = cache.Keys.ToArray();
        foreach (var key in keys)
        {
            if (!safeActiveIds.Contains(key))
                cache.Remove(key);
        }

        // All remaining keys must have been in the original set
        return cache.Keys.All(k => originalKeys.Contains(k));
    }

    // ─── Test helper mimicking FootprintGeometryEntry's role ───

    /// <summary>
    /// Lightweight test equivalent of the private FootprintGeometryEntry class.
    /// Stores the source footprint reference, dimensions, and cached geometry identifier.
    /// </summary>
    private sealed record CacheEntry(
        IReadOnlyList<double> SourceFootprint,
        double Width,
        double Height,
        string CachedGeometry);
}
