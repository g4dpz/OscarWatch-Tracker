using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

/// <summary>
/// Thread-local collection manager for tracking operations to eliminate 
/// temporary allocations in the GetLiveStates method.
/// </summary>
internal static class TrackingCollections
{
    private const int InitialStaleTracksCapacity = 64;
    private const int InitialStateBufferCapacity = 128;
    
    [ThreadStatic]
    internal static List<(SatelliteCatalogEntry Sat, SatelliteVisualCache.Entry Cache)>? _staleTracksBuffer;
    
    [ThreadStatic]
    internal static List<SatelliteTrackState>? _stateBuffer;
    
    [ThreadStatic]
    private static CollectionStatistics? _statistics;
    
    /// <summary>
    /// Get a cleared buffer for collecting stale ground tracks during staggered recomputation.
    /// </summary>
    public static List<(SatelliteCatalogEntry Sat, SatelliteVisualCache.Entry Cache)> GetStaleTracksBuffer()
    {
        _staleTracksBuffer ??= new List<(SatelliteCatalogEntry, SatelliteVisualCache.Entry)>(InitialStaleTracksCapacity);
        _staleTracksBuffer.Clear();
        
        // Update statistics
        var stats = GetStatistics();
        stats.StaleTracksBufferUsageCount++;
        if (_staleTracksBuffer.Capacity > stats.MaxStaleTracksCapacity)
            stats.MaxStaleTracksCapacity = _staleTracksBuffer.Capacity;
            
        return _staleTracksBuffer;
    }
    
    /// <summary>
    /// Get a cleared buffer for collecting satellite states during tracking operations.
    /// </summary>
    public static List<SatelliteTrackState> GetStateBuffer()
    {
        _stateBuffer ??= new List<SatelliteTrackState>(InitialStateBufferCapacity);
        _stateBuffer.Clear();
        
        // Update statistics
        var stats = GetStatistics();
        stats.StateBufferUsageCount++;
        if (_stateBuffer.Capacity > stats.MaxStateBufferCapacity)
            stats.MaxStateBufferCapacity = _stateBuffer.Capacity;
            
        return _stateBuffer;
    }
    
    /// <summary>
    /// Get statistics about thread-local collection usage for monitoring and tuning.
    /// </summary>
    public static CollectionStatistics GetStatistics()
    {
        return _statistics ??= new CollectionStatistics();
    }
    
    /// <summary>
    /// Reset statistics for the current thread. Useful for periodic monitoring.
    /// </summary>
    public static void ResetStatistics()
    {
        _statistics = new CollectionStatistics();
    }
    
    /// <summary>
    /// Compact oversized collections to prevent excessive memory usage.
    /// Call this periodically when collection usage is consistently low.
    /// </summary>
    public static void CompactCollectionsIfOversized()
    {
        CompactListIfOversized(_staleTracksBuffer, InitialStaleTracksCapacity);
        CompactListIfOversized(_stateBuffer, InitialStateBufferCapacity);
    }
    
    private static void CompactListIfOversized<T>(List<T>? list, int initialCapacity)
    {
        if (list is null)
            return;
            
        // If capacity is more than 4x the initial size, trim it back
        if (list.Capacity > initialCapacity * 4)
        {
            list.TrimExcess();
            list.Capacity = Math.Max(initialCapacity, list.Count + 10);
        }
    }
}

/// <summary>
/// Statistics about thread-local collection usage for monitoring and tuning.
/// </summary>
public sealed class CollectionStatistics
{
    public int StaleTracksBufferUsageCount { get; set; }
    public int StateBufferUsageCount { get; set; }
    public int MaxStaleTracksCapacity { get; set; } = 64; // InitialStaleTracksCapacity
    public int MaxStateBufferCapacity { get; set; } = 128; // InitialStateBufferCapacity
    
    /// <summary>
    /// Current capacity of the stale tracks buffer, or 0 if not initialized.
    /// </summary>
    public int CurrentStaleTracksCapacity => TrackingCollections._staleTracksBuffer?.Capacity ?? 0;
    
    /// <summary>
    /// Current capacity of the state buffer, or 0 if not initialized.
    /// </summary>
    public int CurrentStateBufferCapacity => TrackingCollections._stateBuffer?.Capacity ?? 0;
}