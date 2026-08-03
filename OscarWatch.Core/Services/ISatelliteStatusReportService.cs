using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public enum SatelliteStatusValue
{
    On,
    Off,
    TelemetryOnly
}

public sealed record SatelliteStatusReportRequest(
    string Satellite,
    string Mode,
    SatelliteStatusValue Status,
    DateTime ObservedAtUtc,
    string? Gridsquare = null,
    string? Client = null);

public static class SatelliteStatusReportFormatting
{
    /// <summary>Reports require elevation at or above this value (degrees).</summary>
    public const double MinimumElevationDeg = -1.0;

    public static bool IsElevationReportable(double? elevationDeg) =>
        elevationDeg is >= MinimumElevationDeg;

    /// <summary>Normalise a Maidenhead gridsquare to 4 or 6 characters for the status API.</summary>
    public static string? NormalizeGridsquare(string? grid)
    {
        if (string.IsNullOrWhiteSpace(grid))
            return null;

        var g = grid.Trim().ToUpperInvariant();
        if (g.Length >= 6)
            return g[..6];
        if (g.Length >= 4)
            return g[..4];
        return null;
    }

    public static SatelliteCommunityStatusKind ParseCommunityStatus(string? status) =>
        (status ?? "").Trim().ToLowerInvariant() switch
        {
            "on" => SatelliteCommunityStatusKind.On,
            "off" => SatelliteCommunityStatusKind.Off,
            "telemetry_only" => SatelliteCommunityStatusKind.TelemetryOnly,
            _ => SatelliteCommunityStatusKind.Unknown
        };
}

public sealed record SatelliteStatusReportResult(
    bool Ok,
    bool Stored,
    string Message,
    int HttpStatusCode);

public sealed record SatelliteStatusTokenTestResult(
    bool Ok,
    string Message,
    int HttpStatusCode);

public interface ISatelliteStatusReportService
{
    Task<SatelliteStatusTokenTestResult> TestTokenAsync(
        SatelliteStatusSettings settings,
        CancellationToken cancellationToken = default);

    Task<SatelliteStatusReportResult> SubmitReportAsync(
        SatelliteStatusSettings settings,
        SatelliteStatusReportRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Public community aggregate (no Bearer). Soft-fails on network errors.</summary>
    Task<SatelliteStatusFetchResult> FetchCommunityAsync(
        SatelliteStatusSettings settings,
        CancellationToken cancellationToken = default);
}
