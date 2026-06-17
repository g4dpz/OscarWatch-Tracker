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

    /// <summary>
    /// Whether the sidebar row should show the REC badge while a pass recording is active.
    /// Matches the bound list row (<see cref="FindPassForRecording"/>) or any in-progress row
    /// that was active when recording started (covers AOS drift after pass-list refresh).
    /// </summary>
    public static bool IsPassRecordingTarget(
        PassInfo pass,
        string? recordingNoradId,
        DateTime? recordingPassAosUtc,
        DateTime? recordingStartedUtc,
        DateTime utcNow,
        bool isRecording)
    {
        if (!isRecording
            || string.IsNullOrEmpty(recordingNoradId)
            || !string.Equals(pass.NoradId, recordingNoradId, StringComparison.Ordinal))
            return false;

        if (utcNow < pass.AosUtc || utcNow > pass.LosUtc)
            return false;

        if (recordingPassAosUtc is not null && pass.AosUtc == recordingPassAosUtc)
            return true;

        return recordingStartedUtc is { } started
            && started >= pass.AosUtc
            && started <= pass.LosUtc;
    }

    private static bool PassesOverlap(PassInfo a, PassInfo b) =>
        string.Equals(a.NoradId, b.NoradId, StringComparison.Ordinal)
        && a.AosUtc <= b.LosUtc
        && b.AosUtc <= a.LosUtc;
}
