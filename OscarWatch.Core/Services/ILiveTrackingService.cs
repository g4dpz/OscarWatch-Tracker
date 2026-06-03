using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

/// <summary>
/// Background propagation of enabled satellites; UI reads the latest snapshot only.
/// </summary>
public interface ILiveTrackingService : IDisposable
{
    /// <summary>UTC time of the last completed snapshot (or <see cref="DateTime.MinValue"/> if none yet).</summary>
    DateTime SnapshotUtc { get; }

    /// <summary>Offset applied to <see cref="DateTime.UtcNow"/> for map/sky propagation. Zero is live.</summary>
    TimeSpan MapTimeOffset { get; set; }

    /// <summary>Latest propagated states. Do not mutate the returned list or its elements.</summary>
    IReadOnlyList<SatelliteTrackState> GetSnapshot();

    /// <summary>Starts the background worker (idempotent).</summary>
    void Start();

    /// <summary>Reloads enabled satellites in the propagator and refreshes the snapshot on the worker.</summary>
    void RequestReload();
}
