namespace OscarWatch.Core.Services;

/// <summary>Community aggregate status for a satellite mode (from public GET).</summary>
public enum SatelliteCommunityStatusKind
{
    Unknown,
    On,
    Off,
    TelemetryOnly
}

public sealed record SatelliteCommunityRecentReport(
    string Callsign,
    string Gridsquare,
    SatelliteCommunityStatusKind Kind,
    DateTime ObservedAtUtc);

public sealed record SatelliteCommunityModeStatus(
    string ModeType,
    SatelliteCommunityStatusKind Kind,
    string? StatusLabel,
    int ReportCount,
    DateTime? NewestReportUtc,
    IReadOnlyList<SatelliteCommunityRecentReport> RecentReports);

public sealed record SatelliteCommunitySatelliteStatus(
    string Name,
    IReadOnlyList<SatelliteCommunityModeStatus> Modes);

public sealed record SatelliteCommunityCatalog(
    IReadOnlyList<SatelliteCommunitySatelliteStatus> Satellites,
    int WindowHours,
    DateTime ServerTimeUtc,
    DateTime FetchedAtUtc)
{
    public SatelliteCommunityModeStatus? TryGetMode(string satelliteName, string modeType)
    {
        if (string.IsNullOrWhiteSpace(satelliteName) || string.IsNullOrWhiteSpace(modeType))
            return null;

        foreach (var sat in Satellites)
        {
            if (!string.Equals(sat.Name, satelliteName.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var mode in sat.Modes)
            {
                if (string.Equals(mode.ModeType, modeType.Trim(), StringComparison.OrdinalIgnoreCase))
                    return mode;
            }
        }

        return null;
    }

    public SatelliteCommunitySatelliteStatus? TryGetSatellite(string satelliteName)
    {
        if (string.IsNullOrWhiteSpace(satelliteName))
            return null;

        foreach (var sat in Satellites)
        {
            if (string.Equals(sat.Name, satelliteName.Trim(), StringComparison.OrdinalIgnoreCase))
                return sat;
        }

        return null;
    }
}

public sealed record SatelliteStatusFetchResult(
    bool Ok,
    bool FeatureUnavailable,
    SatelliteCommunityCatalog? Catalog,
    string Message,
    int HttpStatusCode);
