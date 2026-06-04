using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public interface IGitHubReleaseService
{
    Task<GitHubLatestRelease> FetchLatestAsync(CancellationToken cancellationToken = default);

    Task<AppUpdateCheckResult> CheckForUpdateAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default);
}
