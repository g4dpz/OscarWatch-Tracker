using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public sealed class SatelliteDatabaseSyncService : ISatelliteDatabaseSyncService
{
    private readonly ISatelliteDatabaseEditor _editor;
    private readonly HttpClient _httpClient;

    public SatelliteDatabaseSyncService(
        ISatelliteDatabaseEditor editor,
        HttpClient? httpClient = null)
    {
        _editor = editor;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<SatelliteDatabaseMergePlan> FetchMergePlanAsync(CancellationToken cancellationToken = default)
    {
        var json = await _httpClient.GetStringAsync(SatelliteDatabasePaths.RemoteDatabaseUrl, cancellationToken)
            .ConfigureAwait(false);
        var remoteEntries = SatelliteDatabaseFile.ParseJson(json);
        var validationError = SatelliteDatabaseFile.ValidateEntries(remoteEntries);
        if (validationError is not null)
            throw new InvalidOperationException($"Remote transponder database is invalid: {validationError}");

        var localEntries = _editor.LoadForEditing();
        return SatelliteDatabaseMerger.BuildPlan(localEntries, remoteEntries);
    }

    public void ApplyMerge(SatelliteDatabaseMergePlan plan, SatelliteDatabaseMergeSelection selection)
    {
        var localEntries = LoadLocalEntriesForMerge();
        SaveMergedEntries(SatelliteDatabaseMerger.Apply(localEntries, plan, selection));
    }

    public List<SatelliteRadioEntry> LoadLocalEntriesForMerge() => _editor.LoadForEditing();

    public void SaveMergedEntries(IReadOnlyList<SatelliteRadioEntry> merged) => _editor.Save(merged);
}
