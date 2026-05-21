using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public interface ITleService
{
    IReadOnlyList<SatelliteCatalogEntry> Catalog { get; }
    DateTime? LastFetchedUtc { get; }
    string CachePath { get; }
    bool IsStale(int staleHours);
    Task RefreshAsync(bool force = false, CancellationToken cancellationToken = default);
    Task EnsureLoadedAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<SatelliteCatalogEntry> GetEnabledSatellites(AppSettings settings);
}
