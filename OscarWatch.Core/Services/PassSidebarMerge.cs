using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

/// <summary>Sidebar pass list helpers — keep in-progress rows across refresh and match recording badges.</summary>
public static class PassSidebarMerge
{
    /// <summary>
    /// Re-adds passes that are still in progress when the predictor omits them (e.g. elevation dipped
    /// below the list threshold before the next 15-minute refresh).
    /// </summary>
    public static IReadOnlyList<PassInfo> MergeInProgressPasses(
        IReadOnlyList<PassInfo> predicted,
        IReadOnlyList<PassInfo> inProgressToRetain,
        DateTime utcNow)
    {
        if (inProgressToRetain.Count == 0)
            return predicted;

        var merged = predicted.ToList();
        foreach (var pass in inProgressToRetain)
        {
            if (pass.AosUtc > utcNow || pass.LosUtc <= utcNow)
                continue;

            if (merged.Any(p => PassesOverlap(p, pass)))
                continue;

            merged.Add(pass);
        }

        return merged.Count == predicted.Count
            ? predicted
            : merged.OrderBy(p => p.AosUtc).ToList();
    }

    /// <summary>
    /// Picks the list row that owns an active recording. Never attaches to a future pass once recording
    /// has already started.
    /// </summary>
    public static PassInfo? FindPassForRecording(
        IReadOnlyList<PassInfo> passes,
        string noradId,
        DateTime utcNow,
        DateTime? recordingStartedUtc)
    {
        var rows = passes
            .Where(p => string.Equals(p.NoradId, noradId, StringComparison.Ordinal))
            .OrderBy(p => p.AosUtc)
            .ToList();

        if (rows.Count == 0)
            return null;

        var inProgress = rows.LastOrDefault(p => utcNow >= p.AosUtc && utcNow <= p.LosUtc);
        if (inProgress is not null)
            return inProgress;

        if (recordingStartedUtc is { } started)
        {
            var atStart = rows.LastOrDefault(p => started >= p.AosUtc && started <= p.LosUtc);
            if (atStart is not null)
                return atStart;

            return null;
        }

        return rows.FirstOrDefault(p => utcNow < p.LosUtc);
    }

    private static bool PassesOverlap(PassInfo a, PassInfo b) =>
        string.Equals(a.NoradId, b.NoradId, StringComparison.Ordinal)
        && a.AosUtc <= b.LosUtc
        && b.AosUtc <= a.LosUtc;
}
