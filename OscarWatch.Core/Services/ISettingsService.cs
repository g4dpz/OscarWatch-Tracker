using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public interface ISettingsService
{
    AppSettings Current { get; }
    string SettingsPath { get; }
    /// <summary>
    /// Non-null when <see cref="Load"/> could not parse the on-disk file.
    /// Persisting is blocked so defaults cannot overwrite the operator's settings.
    /// </summary>
    string? LoadError { get; }
    /// <summary>False after a failed load until an explicit import/replace succeeds.</summary>
    bool CanPersist { get; }
    string SerializeCurrent();
    Task ReplaceAndSaveAsync(AppSettings imported, CancellationToken cancellationToken = default);
    void Load();
    Task LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
    void RequestSave();
    Task FlushAsync(CancellationToken cancellationToken = default);
    void SyncGridFromLatLon();
    void SyncLatLonFromGrid();
    void EnsureSavedStations();
    void ApplyActiveStation();
    void SyncActiveStationFromGroundStation();
}
