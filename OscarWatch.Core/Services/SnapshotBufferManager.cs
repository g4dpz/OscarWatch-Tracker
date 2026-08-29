using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

/// <summary>
/// Manages reusable buffers for satellite tracking snapshots to eliminate 
/// array allocation in PublishSnapshot calls (4-8 arrays per second).
/// </summary>
internal sealed class SnapshotBufferManager
{
    private const int InitialBufferSize = 64;
    private const double GrowthFactor = 1.5;
    private const int MaxBufferSize = 2048;
    
    private SatelliteTrackState[] _displayBuffer = new SatelliteTrackState[InitialBufferSize];
    private SatelliteTrackState[] _liveNowBuffer = new SatelliteTrackState[InitialBufferSize];
    private int _displayCount;
    private int _liveNowCount;
    
    // Statistics for monitoring and tuning
    private int _displayGrowthCount;
    private int _liveNowGrowthCount;
    private int _displayPublishCount;
    private int _liveNowPublishCount;
    
    /// <summary>
    /// Publish a display snapshot using the reusable display buffer.
    /// Returns an immutable copy to prevent data races.
    /// </summary>
    public IReadOnlyList<SatelliteTrackState> PublishDisplaySnapshot(IReadOnlyList<SatelliteTrackState> states)
    {
        if (states.Count == 0)
            return Array.Empty<SatelliteTrackState>();
            
        EnsureDisplayCapacity(states.Count);
        CopyStates(states, _displayBuffer);
        _displayCount = states.Count;
        _displayPublishCount++;
        
        // Create immutable copy to prevent data races - UI can hold this across ticks
        var snapshot = new SatelliteTrackState[_displayCount];
        Array.Copy(_displayBuffer, snapshot, _displayCount);
        return snapshot;
    }
    
    /// <summary>
    /// Publish a live-now snapshot using the reusable live-now buffer.
    /// Returns an immutable copy to prevent data races.
    /// </summary>
    public IReadOnlyList<SatelliteTrackState> PublishLiveNowSnapshot(IReadOnlyList<SatelliteTrackState> states)
    {
        if (states.Count == 0)
            return Array.Empty<SatelliteTrackState>();
            
        EnsureLiveNowCapacity(states.Count);
        CopyStates(states, _liveNowBuffer);
        _liveNowCount = states.Count;
        _liveNowPublishCount++;
        
        // Create immutable copy to prevent data races - UI can hold this across ticks
        var snapshot = new SatelliteTrackState[_liveNowCount];
        Array.Copy(_liveNowBuffer, snapshot, _liveNowCount);
        return snapshot;
    }
    
    /// <summary>
    /// Get statistics about buffer usage for monitoring and tuning.
    /// </summary>
    public SnapshotBufferStatistics GetStatistics()
    {
        return new SnapshotBufferStatistics
        {
            DisplayBufferSize = _displayBuffer.Length,
            LiveNowBufferSize = _liveNowBuffer.Length,
            DisplayGrowthCount = _displayGrowthCount,
            LiveNowGrowthCount = _liveNowGrowthCount,
            DisplayPublishCount = _displayPublishCount,
            LiveNowPublishCount = _liveNowPublishCount,
            CurrentDisplayCount = _displayCount,
            CurrentLiveNowCount = _liveNowCount
        };
    }
    
    /// <summary>
    /// Compact oversized buffers to prevent excessive memory usage.
    /// Call this periodically when satellite count is consistently low.
    /// </summary>
    public void CompactBuffersIfOversized()
    {
        CompactBufferIfOversized(ref _displayBuffer, _displayCount);
        CompactBufferIfOversized(ref _liveNowBuffer, _liveNowCount);
    }
    
    private void EnsureDisplayCapacity(int requiredCount)
    {
        if (_displayBuffer.Length < requiredCount)
        {
            var newSize = CalculateGrowthSize(_displayBuffer.Length, requiredCount);
            Array.Resize(ref _displayBuffer, newSize);
            _displayGrowthCount++;
        }
    }
    
    private void EnsureLiveNowCapacity(int requiredCount)
    {
        if (_liveNowBuffer.Length < requiredCount)
        {
            var newSize = CalculateGrowthSize(_liveNowBuffer.Length, requiredCount);
            Array.Resize(ref _liveNowBuffer, newSize);
            _liveNowGrowthCount++;
        }
    }
    
    private static int CalculateGrowthSize(int currentSize, int requiredCount)
    {
        var growthSize = (int)(currentSize * GrowthFactor);
        var targetSize = Math.Max(requiredCount, growthSize);
        
        // Ensure we never return a size smaller than requiredCount, even if it exceeds MaxBufferSize
        // Better to exceed the limit than cause IndexOutOfRangeException
        return requiredCount > MaxBufferSize ? requiredCount : Math.Min(targetSize, MaxBufferSize);
    }
    
    private static void CopyStates(IReadOnlyList<SatelliteTrackState> source, SatelliteTrackState[] destination)
    {
        // Fast path for common collections
        if (source is SatelliteTrackState[] sourceArray)
        {
            Array.Copy(sourceArray, destination, source.Count);
            return;
        }
        
        if (source is List<SatelliteTrackState> sourceList)
        {
            sourceList.CopyTo(destination);
            return;
        }
        
        // Fallback for other collection types
        for (int i = 0; i < source.Count; i++)
        {
            destination[i] = source[i];
        }
    }
    
    private static void CompactBufferIfOversized(ref SatelliteTrackState[] buffer, int currentUsage)
    {
        // If buffer is more than 4x larger than current usage and larger than initial size
        if (buffer.Length > InitialBufferSize && buffer.Length > currentUsage * 4)
        {
            var newSize = Math.Max(InitialBufferSize, (int)(currentUsage * 1.5));
            Array.Resize(ref buffer, newSize);
        }
    }
}

/// <summary>
/// Statistics about snapshot buffer usage for monitoring and tuning.
/// </summary>
public sealed class SnapshotBufferStatistics
{
    public int DisplayBufferSize { get; init; }
    public int LiveNowBufferSize { get; init; }
    public int DisplayGrowthCount { get; init; }
    public int LiveNowGrowthCount { get; init; }
    public int DisplayPublishCount { get; init; }
    public int LiveNowPublishCount { get; init; }
    public int CurrentDisplayCount { get; init; }
    public int CurrentLiveNowCount { get; init; }
    
    /// <summary>
    /// Average buffer utilization for display snapshots.
    /// </summary>
    public double DisplayUtilization => DisplayBufferSize > 0 ? (double)CurrentDisplayCount / DisplayBufferSize : 0.0;
    
    /// <summary>
    /// Average buffer utilization for live-now snapshots.
    /// </summary>
    public double LiveNowUtilization => LiveNowBufferSize > 0 ? (double)CurrentLiveNowCount / LiveNowBufferSize : 0.0;
}