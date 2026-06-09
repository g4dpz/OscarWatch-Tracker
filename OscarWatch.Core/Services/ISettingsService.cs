using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public interface ISettingsService
{
    AppSettings Current { get; }
    string SettingsPath { get; }
    string SerializeCurrent();
    Task ReplaceAndSaveAsync(AppSettings imported, CancellationToken cancellationToken = default);
    void Load();
    Task LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
    void RequestSave();
    void SyncGridFromLatLon();
    void SyncLatLonFromGrid();
    void EnsureSavedStations();
    void ApplyActiveStation();
    void SyncActiveStationFromGroundStation();
}
