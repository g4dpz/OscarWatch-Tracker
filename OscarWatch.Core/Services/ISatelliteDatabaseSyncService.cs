using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public interface ISatelliteDatabaseSyncService
{
    Task<SatelliteDatabaseMergePlan> FetchMergePlanAsync(CancellationToken cancellationToken = default);

    void ApplyMerge(SatelliteDatabaseMergePlan plan, SatelliteDatabaseMergeSelection selection);

    void SaveMergeAcknowledgments(SatelliteDatabaseMergePlan plan, SatelliteDatabaseMergeSelection selection);

    List<SatelliteRadioEntry> LoadLocalEntriesForMerge();

    void SaveMergedEntries(IReadOnlyList<SatelliteRadioEntry> merged);
}
