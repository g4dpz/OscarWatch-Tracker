using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public enum AppUpdateCheckResultKind
{
    UpToDate,
    UpdateAvailable,
    CheckFailed
}

public sealed class AppUpdateCheckResult
{
    public AppUpdateCheckResultKind Kind { get; init; }
    public GitHubLatestRelease? Release { get; init; }
    public Exception? Error { get; init; }

    public static AppUpdateCheckResult UpToDate() =>
        new() { Kind = AppUpdateCheckResultKind.UpToDate };

    public static AppUpdateCheckResult Available(GitHubLatestRelease release) =>
        new() { Kind = AppUpdateCheckResultKind.UpdateAvailable, Release = release };

    public static AppUpdateCheckResult Failed(Exception error) =>
        new() { Kind = AppUpdateCheckResultKind.CheckFailed, Error = error };
}
