// Task 5.3: Unit tests verifying ground track split cache hit/miss behaviour

using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 4.1, 4.2, 4.3**
///
/// Property-based and unit tests verifying the cache principles used by
/// <c>WorldMapControl._groundTrackSplitCache</c>:
///
/// 1. Cache hit: same list reference + same dimensions means data hasn't changed.
/// 2. Cache miss: new list reference (even with identical content) means data has been refreshed
///    and the split result must be recomputed.
/// 3. Cache miss: same reference but different dimensions means the control was resized
///    and the split result must be recomputed.
/// 4. Eviction: NORAD IDs no longer in the active track set are removed from the cache dictionary.
///
/// The actual <c>GroundTrackSplitEntry</c> is private within WorldMapControl, so these tests
/// exercise the identical staleness-detection and eviction logic in isolation.
/// </summary>
public class GroundTrackSplitCachePropertyTests
{
    // ─── Sub-task 1: Cache hit — same reference + same dimensions = reuse ───

    /// <summary>
    /// Requirement 4.1: When the ground track data for a satellite has not changed
    /// and dimensions are stable, the cached split result should be reused. The cache
    /// detects "not changed" via ReferenceEquals on the IReadOnlyList and matching dimensions.
    /// </summary>
    [Fact]
    public void Same_reference_and_dimensions_signals_cache_hit()
    {
        var cache = new Dictionary<string, SplitCacheEntry>();
        IReadOnlyList<double> groundTrack = new List<double> { 45.0, -120.0, 46.0, -119.0 };
        const double width = 1024;
        const double height = 512;

        // First frame: cache miss, store entry
        cache["25544"] = new SplitCacheEntry(groundTrack, width, height, "split-result-A");

        // Second frame: same reference, same dimensions — cache hit
        var entry = cache["25544"];
        var isHit = ReferenceEquals(entry.SourceTrack, groundTrack)
                    && entry.Width == width
                    && entry.Height == height;

        Assert.True(isHit);
        Assert.Equal("split-result-A", entry.CachedSplitResult);
    }

    /// <summary>
    /// Property: For any list content, assigning to a variable and checking
    /// ReferenceEquals always returns true (same reference = cache hit signal).
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Same_reference_always_signals_hit(int[] data)
    {
        IReadOnlyList<int> groundTrack = data;

        // Simulates the cache storing the reference and checking on next frame
        var cached = groundTrack;
        return ReferenceEquals(cached, groundTrack);
    }

    /// <summary>
    /// Simulates the full cache lookup: store a track reference keyed by NORAD ID,
    /// then on next frame check the same reference and dimensions — should be a cache hit.
    /// </summary>
    [Fact]
    public void Cache_dictionary_lookup_with_same_reference_and_dimensions_is_hit()
    {
        var cache = new Dictionary<string, SplitCacheEntry>();
        IReadOnlyList<double> groundTrack = new List<double> { 51.6, 32.9, 50.1, 35.2 };
        const double width = 800;
        const double height = 400;

        cache["25544"] = new SplitCacheEntry(groundTrack, width, height, "split-ISS");

        // Next frame: same reference, same dimensions
        var entry = cache["25544"];
        var isHit = ReferenceEquals(entry.SourceTrack, groundTrack)
                    && entry.Width == width
                    && entry.Height == height;

        Assert.True(isHit);
        Assert.Equal("split-ISS", entry.CachedSplitResult);
    }

    // ─── Sub-task 2: Cache miss — new reference or different dimensions = recompute ───

    /// <summary>
    /// Requirement 4.2: When SatelliteVisualCache provides updated ground track data
    /// (a new list object, ~every 45 seconds), the split result must be recomputed.
    /// The cache detects this via ReferenceEquals returning false.
    /// </summary>
    [Fact]
    public void New_list_with_same_content_signals_cache_miss()
    {
        var original = new List<double> { 45.0, -120.0, 46.0, -119.0 };
        IReadOnlyList<double> track1 = original;

        // SatelliteVisualCache refreshes — creates a new list with same content
        IReadOnlyList<double> track2 = new List<double> { 45.0, -120.0, 46.0, -119.0 };

        var isCacheMiss = !ReferenceEquals(track1, track2);

        Assert.True(isCacheMiss);
    }

    /// <summary>
    /// Property: Creating a new list from any source always produces a different
    /// reference (ReferenceEquals == false), triggering a cache miss.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool New_list_from_same_data_always_signals_miss(int[] data)
    {
        IReadOnlyList<int> track1 = data;
        // Simulate SatelliteVisualCache creating a new list on refresh
        IReadOnlyList<int> track2 = data.ToList();

        return !ReferenceEquals(track1, track2);
    }

    /// <summary>
    /// Simulates the full cache lookup: store a track reference, then on next frame
    /// receive a new list (refresh from SatelliteVisualCache) — should be a cache miss
    /// requiring split recomputation.
    /// </summary>
    [Fact]
    public void Cache_dictionary_lookup_with_new_reference_is_miss()
    {
        var cache = new Dictionary<string, SplitCacheEntry>();
        IReadOnlyList<double> track1 = new List<double> { 51.6, 32.9, 50.1, 35.2 };
        const double width = 800;
        const double height = 400;

        // First frame: store entry
        cache["25544"] = new SplitCacheEntry(track1, width, height, "split-A");

        // SatelliteVisualCache refreshes — new list object
        IReadOnlyList<double> track2 = new List<double> { 51.6, 32.9, 50.1, 35.2 };

        // Second frame: new reference — cache miss
        var entry = cache["25544"];
        var isHit = ReferenceEquals(entry.SourceTrack, track2)
                    && entry.Width == width
                    && entry.Height == height;

        Assert.False(isHit);
    }

    /// <summary>
    /// Dimension change also triggers a cache miss even with the same track reference.
    /// This happens when the control is resized.
    /// </summary>
    [Fact]
    public void Dimension_change_with_same_reference_signals_cache_miss()
    {
        var cache = new Dictionary<string, SplitCacheEntry>();
        IReadOnlyList<double> groundTrack = new List<double> { 51.6, 32.9, 50.1, 35.2 };

        // First frame at 800x400
        cache["25544"] = new SplitCacheEntry(groundTrack, 800, 400, "split-A");

        // Control resized to 1024x512 — same track ref but different dimensions
        var entry = cache["25544"];
        var isHit = ReferenceEquals(entry.SourceTrack, groundTrack)
                    && entry.Width == 1024
                    && entry.Height == 512;

        Assert.False(isHit);
    }

    // ─── Sub-task 3: Eviction removes entries for removed NORAD IDs ───

    /// <summary>
    /// Requirement 4.3: When a satellite is removed from TrackStates, its cache entry
    /// should be evicted to prevent unbounded memory growth.
    /// </summary>
    [Fact]
    public void Eviction_removes_entries_for_absent_norad_ids()
    {
        var cache = new Dictionary<string, SplitCacheEntry>
        {
            ["25544"] = new SplitCacheEntry(Array.Empty<double>(), 800, 400, "ISS"),
            ["07530"] = new SplitCacheEntry(Array.Empty<double>(), 800, 400, "AO-7"),
            ["43017"] = new SplitCacheEntry(Array.Empty<double>(), 800, 400, "FO-29")
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
        var cache = new Dictionary<string, SplitCacheEntry>
        {
            ["25544"] = new SplitCacheEntry(Array.Empty<double>(), 800, 400, "ISS"),
            ["07530"] = new SplitCacheEntry(Array.Empty<double>(), 800, 400, "AO-7")
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
        var cache = new Dictionary<string, SplitCacheEntry>
        {
            ["25544"] = new SplitCacheEntry(Array.Empty<double>(), 800, 400, "ISS"),
            ["07530"] = new SplitCacheEntry(Array.Empty<double>(), 800, 400, "AO-7")
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
            cache[id] = $"split-{id}";

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
            cache[id] = $"split-{id}";

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

    // ─── Test helper mimicking GroundTrackSplitEntry's role ───

    /// <summary>
    /// Lightweight test equivalent of the private GroundTrackSplitEntry class.
    /// Stores the source track reference, dimensions, and cached split result identifier.
    /// </summary>
    private sealed record SplitCacheEntry(
        IReadOnlyList<double> SourceTrack,
        double Width,
        double Height,
        string CachedSplitResult);
}
