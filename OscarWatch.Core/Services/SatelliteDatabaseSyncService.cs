using OscarWatch.Core.Models;
using OscarWatch.Core.Net;

namespace OscarWatch.Core.Services;

public sealed class SatelliteDatabaseSyncService : ISatelliteDatabaseSyncService
{
    private readonly ISatelliteDatabaseEditor _editor;
    private readonly ISettingsService _settings;
    private readonly HttpClient _httpClient;

    public SatelliteDatabaseSyncService(
        ISatelliteDatabaseEditor editor,
        ISettingsService settings,
        HttpClient? httpClient = null)
    {
        _editor = editor;
        _settings = settings;
        _httpClient = httpClient ?? OscarWatchHttpClients.Create(TimeSpan.FromSeconds(30));
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
        var plan = SatelliteDatabaseMerger.BuildPlan(localEntries, remoteEntries);
        return SatelliteDatabaseMerger.WithoutAcknowledgedConflicts(
            plan,
            _settings.Current.TransponderConflictAcknowledgments);
    }

    public void ApplyMerge(SatelliteDatabaseMergePlan plan, SatelliteDatabaseMergeSelection selection)
    {
        var localEntries = LoadLocalEntriesForMerge();
        SaveMergedEntries(SatelliteDatabaseMerger.Apply(localEntries, plan, selection));
        SaveMergeAcknowledgments(plan, selection);
    }

    public void SaveMergeAcknowledgments(SatelliteDatabaseMergePlan plan, SatelliteDatabaseMergeSelection selection)
    {
        var acknowledgments = _settings.Current.TransponderConflictAcknowledgments;
        SatelliteDatabaseMerger.UpsertLocalAcknowledgments(
            acknowledgments,
            SatelliteDatabaseMerger.BuildLocalAcknowledgments(plan, selection));
        SatelliteDatabaseMerger.RemoveAcknowledgments(acknowledgments, selection.AcceptRemoteConflictKeys);
        _settings.RequestSave();
    }

    public List<SatelliteRadioEntry> LoadLocalEntriesForMerge() => _editor.LoadForEditing();

    public void SaveMergedEntries(IReadOnlyList<SatelliteRadioEntry> merged) => _editor.Save(merged);
}
