using System.Collections.Concurrent;
using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

/// <summary>
/// Thread-local object pool for SatelliteTrackState instances to eliminate allocations 
/// in the real-time tracking loop (250ms execution frequency).
/// </summary>
internal static class SatelliteTrackStatePool
{
    private const int InitialPoolSize = 128;
    private const int MaxPoolSize = 512;
    
    [ThreadStatic]
    private static Pool? _pool;
    
    /// <summary>
    /// Rent a SatelliteTrackState object from the thread-local pool.
    /// </summary>
    public static SatelliteTrackState Rent()
    {
        _pool ??= new Pool();
        return _pool.Rent();
    }
    
    /// <summary>
    /// Return a SatelliteTrackState object to the thread-local pool after resetting it.
    /// </summary>
    public static void Return(SatelliteTrackState state)
    {
        if (_pool is null)
            return;
            
        state.Reset();
        _pool.Return(state);
    }
    
    /// <summary>
    /// Return multiple SatelliteTrackState objects to the pool.
    /// </summary>
    public static void ReturnRange(IReadOnlyList<SatelliteTrackState> states)
    {
        if (_pool is null)
            return;
            
        for (int i = 0; i < states.Count; i++)
        {
            var state = states[i];
            state.Reset();
            _pool.Return(state);
        }
    }
    
    /// <summary>
    /// Get statistics about the current thread's pool usage.
    /// </summary>
    public static PoolStatistics GetStatistics()
    {
        return _pool?.GetStatistics() ?? new PoolStatistics();
    }
    
    private sealed class Pool
    {
        private readonly ConcurrentStack<SatelliteTrackState> _objects = new();
        private int _rentCount;
        private int _returnCount;
        private int _createCount;
        private int _currentSize;
        
        public Pool()
        {
            // Pre-allocate initial pool size with required members initialized
            for (int i = 0; i < InitialPoolSize; i++)
            {
                var obj = new SatelliteTrackState
                {
                    Name = string.Empty,
                    NoradId = string.Empty,
                    Subpoint = new GeoCoordinate(0, 0, 0)
                };
                obj.Reset(); // Ensure clean initial state
                _objects.Push(obj);
            }
            _currentSize = InitialPoolSize;
            _createCount = InitialPoolSize;
        }
        
        public SatelliteTrackState Rent()
        {
            Interlocked.Increment(ref _rentCount);
            
            if (_objects.TryPop(out var obj))
            {
                Interlocked.Decrement(ref _currentSize);
                return obj;
            }
            
            // Pool exhausted, create new object with required members
            Interlocked.Increment(ref _createCount);
            return new SatelliteTrackState
            {
                Name = string.Empty,
                NoradId = string.Empty,
                Subpoint = new GeoCoordinate(0, 0, 0)
            };
        }
        
        public void Return(SatelliteTrackState obj)
        {
            Interlocked.Increment(ref _returnCount);
            
            // Don't return to pool if we're at max capacity
            var currentSize = Volatile.Read(ref _currentSize);
            if (currentSize >= MaxPoolSize)
                return;
                
            _objects.Push(obj);
            Interlocked.Increment(ref _currentSize);
        }
        
        public PoolStatistics GetStatistics()
        {
            return new PoolStatistics
            {
                CurrentSize = Volatile.Read(ref _currentSize),
                RentCount = Volatile.Read(ref _rentCount),
                ReturnCount = Volatile.Read(ref _returnCount),
                CreateCount = Volatile.Read(ref _createCount),
                MaxPoolSize = MaxPoolSize
            };
        }
    }
}

/// <summary>
/// Statistics about object pool usage for monitoring and tuning.
/// </summary>
public sealed class PoolStatistics
{
    public int CurrentSize { get; init; }
    public int RentCount { get; init; }
    public int ReturnCount { get; init; }
    public int CreateCount { get; init; }
    public int MaxPoolSize { get; init; }
    
    /// <summary>
    /// Percentage of rent operations satisfied by the pool (vs creating new objects).
    /// </summary>
    public double HitRatio => RentCount > 0 ? (double)(RentCount - CreateCount) / RentCount : 0.0;
    
    /// <summary>
    /// Current pool utilization as a percentage of max capacity.
    /// </summary>
    public double UtilizationPercentage => MaxPoolSize > 0 ? (double)CurrentSize / MaxPoolSize : 0.0;
}