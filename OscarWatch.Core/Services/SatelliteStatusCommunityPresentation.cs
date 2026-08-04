using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public static class SatelliteStatusCommunityPresentation
{
    /// <summary>How long a successful fetch may be shown / kept after a soft failure.</summary>
    public static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How often to poll the community status API.
    /// Shorter than <see cref="CacheTtl"/> so a timer tick refetches before the display cache expires.
    /// </summary>
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(3);

    public static bool IsStale(DateTime? newestReportUtc, DateTime utcNow) =>
        newestReportUtc is null || utcNow - newestReportUtc.Value.ToUniversalTime() > StaleAfter;

    public static bool IsCacheFresh(DateTime fetchedAtUtc, DateTime utcNow) =>
        utcNow - fetchedAtUtc.ToUniversalTime() <= CacheTtl;

    /// <summary>True when a new network fetch should run (cache age at or past the refresh interval).</summary>
    public static bool IsRefreshDue(DateTime fetchedAtUtc, DateTime utcNow) =>
        utcNow - fetchedAtUtc.ToUniversalTime() >= RefreshInterval;

    public static string ShortLabel(SatelliteCommunityStatusKind kind) => kind switch
    {
        SatelliteCommunityStatusKind.On => "On",
        SatelliteCommunityStatusKind.Off => "Off",
        SatelliteCommunityStatusKind.TelemetryOnly => "Telem.",
        _ => "?"
    };

    public static string ShortLabel(
        SatelliteCommunityStatusKind kind,
        Func<string, object?[], string> localize) => kind switch
    {
        SatelliteCommunityStatusKind.On => localize("SatStatus.Community.Short.On", []),
        SatelliteCommunityStatusKind.Off => localize("SatStatus.Community.Short.Off", []),
        SatelliteCommunityStatusKind.TelemetryOnly => localize("SatStatus.Community.Short.Telem", []),
        _ => localize("SatStatus.Community.Short.Unknown", [])
    };

    public static string FullLabel(SatelliteCommunityStatusKind kind, string? statusLabel)
    {
        if (!string.IsNullOrWhiteSpace(statusLabel))
            return statusLabel.Trim();

        return kind switch
        {
            SatelliteCommunityStatusKind.On => "On",
            SatelliteCommunityStatusKind.Off => "Off",
            SatelliteCommunityStatusKind.TelemetryOnly => "Telemetry only",
            _ => "No recent reports"
        };
    }

    public static string FullLabel(
        SatelliteCommunityStatusKind kind,
        string? statusLabel,
        Func<string, object?[], string> localize)
    {
        // Prefer localised labels so UI language matches; API status_label is English-only.
        _ = statusLabel;
        return kind switch
        {
            SatelliteCommunityStatusKind.On => localize("SatStatus.Report.Status.On", []),
            SatelliteCommunityStatusKind.Off => localize("SatStatus.Report.Status.Off", []),
            SatelliteCommunityStatusKind.TelemetryOnly => localize("SatStatus.Report.Status.TelemetryOnly", []),
            _ => localize("SatStatus.Community.NoRecentReports", [])
        };
    }

    public static string FormatAge(DateTime? newestReportUtc, DateTime utcNow)
    {
        if (newestReportUtc is null)
            return "";

        var age = utcNow - newestReportUtc.Value.ToUniversalTime();
        if (age < TimeSpan.Zero)
            age = TimeSpan.Zero;

        if (age < TimeSpan.FromMinutes(1))
            return "just now";
        if (age < TimeSpan.FromHours(1))
        {
            var minutes = Math.Max(1, (int)age.TotalMinutes);
            return $"{minutes}m ago";
        }

        if (age < TimeSpan.FromDays(1))
        {
            var hours = Math.Max(1, (int)age.TotalHours);
            return $"{hours}h ago";
        }

        var days = Math.Max(1, (int)age.TotalDays);
        return $"{days}d ago";
    }

    /// <summary>
    /// Resolve which mode should colour a pass-row dot.
    /// </summary>
    public static string? ResolvePassRowModeType(
        string satelliteName,
        string? noradId,
        IReadOnlyDictionary<string, SatelliteFrequencySelection> frequencySelections,
        ISatelliteDatabaseService database)
    {
        if (string.IsNullOrWhiteSpace(satelliteName))
            return null;

        var entry = database.TryGetEntry(satelliteName, noradId);
        var storageKey = entry?.Name?.Trim() ?? satelliteName.Trim();

        if (frequencySelections.TryGetValue(storageKey, out var selection)
            && !string.IsNullOrWhiteSpace(selection.ModeType))
        {
            return selection.ModeType.Trim();
        }

        if (!string.Equals(storageKey, satelliteName.Trim(), StringComparison.OrdinalIgnoreCase)
            && frequencySelections.TryGetValue(satelliteName.Trim(), out selection)
            && !string.IsNullOrWhiteSpace(selection.ModeType))
        {
            return selection.ModeType.Trim();
        }

        if (entry?.Modes is null || entry.Modes.Count == 0)
            return null;

        var nonBeacon = entry.Modes.FirstOrDefault(m => !m.IsBeaconOnly && !string.IsNullOrWhiteSpace(m.Type));
        if (nonBeacon is not null)
            return nonBeacon.Type.Trim();

        var any = entry.Modes.FirstOrDefault(m => !string.IsNullOrWhiteSpace(m.Type));
        return any?.Type.Trim();
    }

    public static IReadOnlyList<string> CatalogueModeTypes(
        string satelliteName,
        string? noradId,
        ISatelliteDatabaseService database)
    {
        var entry = database.TryGetEntry(satelliteName, noradId);
        if (entry?.Modes is null || entry.Modes.Count == 0)
            return [];

        return entry.Modes
            .Select(m => m.Type?.Trim() ?? "")
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string BuildOverlayToolTip(
        SatelliteCommunityModeStatus mode,
        int windowHours,
        DateTime utcNow,
        Func<string, object?[], string> localize)
    {
        var lines = new List<string>
        {
            localize("SatStatus.Community.OverlayTitle", []),
            FullLabel(mode.Kind, mode.StatusLabel, localize)
        };

        if (mode.ReportCount > 0)
            lines.Add(localize("SatStatus.Community.ReportCount", [mode.ReportCount]));

        var age = FormatAge(mode.NewestReportUtc, utcNow);
        if (!string.IsNullOrEmpty(age))
            lines.Add(localize("SatStatus.Community.LastReport", [age]));

        if (IsStale(mode.NewestReportUtc, utcNow) && mode.Kind != SatelliteCommunityStatusKind.Unknown)
            lines.Add(localize("SatStatus.Community.StaleNote", []));

        lines.Add(localize("SatStatus.Community.WindowNote", [windowHours]));
        return string.Join(Environment.NewLine, lines);
    }

    public static string BuildPassToolTip(
        string satelliteName,
        string? dotModeType,
        bool dotFromSelection,
        IReadOnlyList<string> catalogueModes,
        SatelliteCommunitySatelliteStatus? communitySat,
        int windowHours,
        DateTime utcNow,
        Func<string, object?[], string> localize)
    {
        var lines = new List<string>
        {
            localize("SatStatus.Community.PassTitle", [satelliteName, windowHours])
        };

        var modesToShow = catalogueModes.Count > 0
            ? catalogueModes
            : communitySat?.Modes.Select(m => m.ModeType).ToList() ?? [];

        if (modesToShow.Count == 0)
        {
            lines.Add(localize("SatStatus.Community.NoModeData", []));
            return string.Join(Environment.NewLine, lines);
        }

        foreach (var modeType in modesToShow)
        {
            var mode = communitySat?.Modes.FirstOrDefault(m =>
                string.Equals(m.ModeType, modeType, StringComparison.OrdinalIgnoreCase));
            var kind = mode?.Kind ?? SatelliteCommunityStatusKind.Unknown;
            var label = FullLabel(kind, mode?.StatusLabel, localize);
            var age = FormatAge(mode?.NewestReportUtc, utcNow);
            var isDot = !string.IsNullOrWhiteSpace(dotModeType)
                        && string.Equals(modeType, dotModeType, StringComparison.OrdinalIgnoreCase);
            var prefix = isDot ? "● " : "  ";
            var line = string.IsNullOrEmpty(age)
                ? $"{prefix}{modeType}: {label}"
                : $"{prefix}{modeType}: {label} ({localize("SatStatus.Community.LastReportInline", [age])})";

            if (isDot)
            {
                line += " · " + (dotFromSelection
                    ? localize("SatStatus.Community.YourSelection", [])
                    : localize("SatStatus.Community.DefaultMode", []));
            }

            lines.Add(line);
        }

        lines.Add(localize("SatStatus.Community.WindowNote", [windowHours]));
        return string.Join(Environment.NewLine, lines);
    }
}
