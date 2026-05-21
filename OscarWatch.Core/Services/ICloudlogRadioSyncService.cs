using OscarWatch.Core.Cloudlog;
using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public interface ICloudlogRadioSyncService
{
    string? LastError { get; }

    DateTimeOffset? LastSuccessUtc { get; }

    void Publish(CloudlogSettings settings, CloudlogRadioUpdate? update);

    void ResetThrottle();

    Task<bool> TestConnectionAsync(CloudlogSettings settings, CancellationToken cancellationToken = default);
}
