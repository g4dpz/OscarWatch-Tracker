using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

/// <summary>
/// Thread-local collection manager for optimizing allocation-heavy LINQ operations in hot paths.
/// Provides pre-allocated collections for PassInfo objects to eliminate garbage collection pressure
/// during frequent tracking operations.
/// </summary>
internal static class HotPathCollections
{
    // Thread-local pre-allocated collections with empirically determined initial capacities
    private static readonly ThreadLocal<List<PassInfo>> _passInfoBuffer = 
        new(() => new List<PassInfo>(64)); // Estimated max passes across all satellites in typical prediction window
    
    private static readonly ThreadLocal<List<PassInfo>> _localPassBuffer = 
        new(() => new List<PassInfo>(32)); // Estimated max passes per site
        
    private static readonly ThreadLocal<List<PassInfo>> _remotePassBuffer = 
        new(() => new List<PassInfo>(32)); // Estimated max passes per site

    /// <summary>
    /// Gets a cleared, thread-local pre-allocated collection for PassInfo objects.
    /// This buffer is intended for general pass processing operations.
    /// Implements graceful degradation under memory pressure by falling back to method-local allocation.
    /// </summary>
    /// <returns>A cleared List&lt;PassInfo&gt; ready for use. The same instance is returned for the same thread across calls.</returns>
    public static List<PassInfo> GetPassInfoBuffer()
    {
        try
        {
            var buffer = _passInfoBuffer.Value;
            if (buffer != null)
            {
                try
                {
                    buffer.Clear();
                    return buffer;
                }
                catch (OutOfMemoryException)
                {
                    // Clear operation failed due to memory pressure
                    // Fall back to method-local allocation
                }
            }
        }
        catch (OutOfMemoryException)
        {
            // ThreadLocal access failed due to memory pressure
        }
        catch (ObjectDisposedException)
        {
            // ThreadLocal was disposed, fall back to method-local allocation
        }
        catch (InvalidOperationException)
        {
            // ThreadLocal initialization failed, fall back to method-local allocation
        }
        
        // Fallback allocation with graceful degradation
        try
        {
            return new List<PassInfo>(64);
        }
        catch (OutOfMemoryException)
        {
            // Even smaller allocation if standard size fails
            try
            {
                return new List<PassInfo>(16);
            }
            catch (OutOfMemoryException)
            {
                // Minimal allocation as last resort
                return new List<PassInfo>();
            }
        }
    }
    
    /// <summary>
    /// Gets a cleared, thread-local pre-allocated collection for local site PassInfo objects.
    /// This buffer is specifically intended for mutual pass calculations.
    /// Implements graceful degradation under memory pressure by falling back to method-local allocation.
    /// </summary>
    /// <returns>A cleared List&lt;PassInfo&gt; ready for use. The same instance is returned for the same thread across calls.</returns>
    public static List<PassInfo> GetLocalPassBuffer()
    {
        try
        {
            var buffer = _localPassBuffer.Value;
            if (buffer != null)
            {
                try
                {
                    buffer.Clear();
                    return buffer;
                }
                catch (OutOfMemoryException)
                {
                    // Clear operation failed due to memory pressure
                    // Fall back to method-local allocation
                }
            }
        }
        catch (OutOfMemoryException)
        {
            // ThreadLocal access failed due to memory pressure
        }
        catch (ObjectDisposedException)
        {
            // ThreadLocal was disposed, fall back to method-local allocation
        }
        catch (InvalidOperationException)
        {
            // ThreadLocal initialization failed, fall back to method-local allocation
        }
        
        // Fallback allocation with graceful degradation
        try
        {
            return new List<PassInfo>(32);
        }
        catch (OutOfMemoryException)
        {
            // Even smaller allocation if standard size fails
            try
            {
                return new List<PassInfo>(8);
            }
            catch (OutOfMemoryException)
            {
                // Minimal allocation as last resort
                return new List<PassInfo>();
            }
        }
    }
    
    /// <summary>
    /// Gets a cleared, thread-local pre-allocated collection for remote site PassInfo objects.
    /// This buffer is specifically intended for mutual pass calculations.
    /// Implements graceful degradation under memory pressure by falling back to method-local allocation.
    /// </summary>
    /// <returns>A cleared List&lt;PassInfo&gt; ready for use. The same instance is returned for the same thread across calls.</returns>
    public static List<PassInfo> GetRemotePassBuffer()
    {
        try
        {
            var buffer = _remotePassBuffer.Value;
            if (buffer != null)
            {
                try
                {
                    buffer.Clear();
                    return buffer;
                }
                catch (OutOfMemoryException)
                {
                    // Clear operation failed due to memory pressure
                    // Fall back to method-local allocation
                }
            }
        }
        catch (OutOfMemoryException)
        {
            // ThreadLocal access failed due to memory pressure
        }
        catch (ObjectDisposedException)
        {
            // ThreadLocal was disposed, fall back to method-local allocation
        }
        catch (InvalidOperationException)
        {
            // ThreadLocal initialization failed, fall back to method-local allocation
        }
        
        // Fallback allocation with graceful degradation
        try
        {
            return new List<PassInfo>(32);
        }
        catch (OutOfMemoryException)
        {
            // Even smaller allocation if standard size fails
            try
            {
                return new List<PassInfo>(8);
            }
            catch (OutOfMemoryException)
            {
                // Minimal allocation as last resort
                return new List<PassInfo>();
            }
        }
    }

    /// <summary>
    /// Disposes all thread-local resources. This method should be called during application shutdown
    /// to ensure proper cleanup of ThreadLocal instances.
    /// </summary>
    public static void Dispose()
    {
        _passInfoBuffer.Dispose();
        _localPassBuffer.Dispose();
        _remotePassBuffer.Dispose();
    }
}