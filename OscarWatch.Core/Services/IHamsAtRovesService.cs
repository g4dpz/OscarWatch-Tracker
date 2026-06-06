using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public sealed class HamsAtFetchResult
{
    public required bool Ok { get; init; }
    public IReadOnlyList<HamsAtUpcomingAlert> Alerts { get; init; } = [];
    public string? ErrorMessage { get; init; }

    public static HamsAtFetchResult Success(IReadOnlyList<HamsAtUpcomingAlert> alerts) =>
        new() { Ok = true, Alerts = alerts };

    public static HamsAtFetchResult Failed(string message) =>
        new() { Ok = false, ErrorMessage = message };
}

public interface IHamsAtRovesService
{
    Task<HamsAtFetchResult> FetchUpcomingAsync(
        HamsAtSettings settings,
        bool bypassCache = false,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, string Message)> TestConnectionAsync(
        HamsAtSettings settings,
        CancellationToken cancellationToken = default);
}
