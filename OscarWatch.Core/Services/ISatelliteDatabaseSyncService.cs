namespace OscarWatch.Core.Services;

public interface ISatelliteDatabaseSyncService
{
    Task<SatelliteDatabaseMergePlan> FetchMergePlanAsync(CancellationToken cancellationToken = default);

    void ApplyMerge(SatelliteDatabaseMergePlan plan, SatelliteDatabaseMergeSelection selection);
}
