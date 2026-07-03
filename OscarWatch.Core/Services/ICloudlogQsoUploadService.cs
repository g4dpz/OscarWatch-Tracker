namespace OscarWatch.Core.Services;

public interface ICloudlogQsoUploadService
{
    Task QueueUploadIfEnabledAsync(long qsoId, CancellationToken cancellationToken = default);

    Task ProcessRetryQueueAsync(CancellationToken cancellationToken = default);

    Task ResetFailedUploadsForRetryAsync(long logbookId, CancellationToken cancellationToken = default);
}
