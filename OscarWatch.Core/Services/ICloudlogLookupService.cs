using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public interface ICloudlogLookupService
{
    bool CanCheckGrids(CloudlogSettings settings);

    Task<CloudlogLogbooksResult> FetchLogbooksAsync(
        CloudlogSettings settings,
        CancellationToken cancellationToken = default);

    Task<CloudlogGridCheckResult?> CheckGridWorkedAsync(
        CloudlogSettings settings,
        string grid,
        CancellationToken cancellationToken = default);

    bool CanUploadQsos(CloudlogSettings settings);

    Task<CloudlogStationProfilesResult> FetchStationProfilesAsync(
        CloudlogSettings settings,
        CancellationToken cancellationToken = default);
}
