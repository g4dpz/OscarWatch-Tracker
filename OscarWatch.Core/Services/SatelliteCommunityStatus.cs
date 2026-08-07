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
    // O(1) lookup index built lazily on first access.
    private Dictionary<string, SatelliteCommunitySatelliteStatus>? _index;

    private Dictionary<string, SatelliteCommunitySatelliteStatus> Index =>
        _index ??= BuildIndex(Satellites);

    // First name wins (case-insensitive), same as the old linear scan; duplicates must not throw.
    private static Dictionary<string, SatelliteCommunitySatelliteStatus> BuildIndex(
        IReadOnlyList<SatelliteCommunitySatelliteStatus> satellites)
    {
        var index = new Dictionary<string, SatelliteCommunitySatelliteStatus>(
            satellites.Count,
            StringComparer.OrdinalIgnoreCase);
        foreach (var sat in satellites)
            index.TryAdd(sat.Name, sat);
        return index;
    }

    public SatelliteCommunityModeStatus? TryGetMode(string satelliteName, string modeType)
    {
        if (string.IsNullOrWhiteSpace(satelliteName) || string.IsNullOrWhiteSpace(modeType))
            return null;

        if (!Index.TryGetValue(satelliteName.Trim(), out var sat))
            return null;

        foreach (var mode in sat.Modes)
        {
            if (string.Equals(mode.ModeType, modeType.Trim(), StringComparison.OrdinalIgnoreCase))
                return mode;
        }

        return null;
    }

    public SatelliteCommunitySatelliteStatus? TryGetSatellite(string satelliteName)
    {
        if (string.IsNullOrWhiteSpace(satelliteName))
            return null;

        return Index.GetValueOrDefault(satelliteName.Trim());
    }
}

public sealed record SatelliteStatusFetchResult(
    bool Ok,
    bool FeatureUnavailable,
    SatelliteCommunityCatalog? Catalog,
    string Message,
    int HttpStatusCode);
