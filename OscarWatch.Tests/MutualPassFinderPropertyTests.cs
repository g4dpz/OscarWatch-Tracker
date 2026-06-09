// Feature: test-coverage-expansion, Property 10: Mutual Pass NORAD ID Invariant
// Feature: test-coverage-expansion, Property 11: Mutual Pass Time Bounds
// Feature: test-coverage-expansion, Property 12: Mutual Pass Duration Threshold
// Feature: test-coverage-expansion, Property 13: Mutual Pass Sorted Output

using FsCheck.Xunit;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 4.1, 4.2, 4.3, 4.4, 4.7**
///
/// Property-based tests verifying overlap invariants of
/// <see cref="MutualPassFinder"/>: NORAD ID consistency, time bounds
/// correctness, minimum duration threshold, and sorted output.
/// </summary>
public class MutualPassFinderPropertyTests
{
    /// <summary>
    /// Property 10: Mutual Pass NORAD ID Invariant.
    ///
    /// For any lists of local and remote passes and any minimum duration,
    /// all entries in the result of FindOverlaps SHALL have
    /// LocalPass.NoradId == RemotePass.NoradId == NoradId.
    /// </summary>
    [Property]
    public bool All_overlaps_have_matching_norad_ids(int aosOffsetMinutes, int durationMinutes1, int durationMinutes2)
    {
        if (!IsFinite(aosOffsetMinutes) || !IsFinite(durationMinutes1) || !IsFinite(durationMinutes2))
            return true;

        // Constrain to reasonable values ensuring valid overlapping passes
        var baseTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var localDuration = Math.Abs(durationMinutes1 % 60) + 10; // at least 10 minutes
        var remoteDuration = Math.Abs(durationMinutes2 % 60) + 10;
        var offset = (aosOffsetMinutes % 30); // remote starts within ±30 min of local

        var noradId = "25544";
        var localPass = CreatePass(noradId, baseTime, TimeSpan.FromMinutes(localDuration));
        var remotePass = CreatePass(noradId, baseTime.AddMinutes(offset), TimeSpan.FromMinutes(remoteDuration));

        // Add a non-matching pass to verify filtering
        var otherPass = CreatePass("99999", baseTime, TimeSpan.FromMinutes(20));

        var localPasses = new List<PassInfo> { localPass, otherPass };
        var remotePasses = new List<PassInfo> { remotePass };

        var results = MutualPassFinder.FindOverlaps(localPasses, remotePasses, TimeSpan.FromMinutes(1));

        return results.All(r =>
            r.NoradId == r.LocalPass.NoradId &&
            r.NoradId == r.RemotePass.NoradId);
    }

    /// <summary>
    /// Property 11: Mutual Pass Time Bounds.
    ///
    /// For any returned MutualPassInfo, MutualStartUtc SHALL equal
    /// max(LocalPass.AosUtc, RemotePass.AosUtc) and MutualEndUtc SHALL
    /// equal min(LocalPass.LosUtc, RemotePass.LosUtc).
    /// </summary>
    [Property]
    public bool Mutual_start_is_max_aos_and_mutual_end_is_min_los(int aosOffsetMinutes, int durationMinutes1, int durationMinutes2)
    {
        if (!IsFinite(aosOffsetMinutes) || !IsFinite(durationMinutes1) || !IsFinite(durationMinutes2))
            return true;

        var baseTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var localDuration = Math.Abs(durationMinutes1 % 60) + 10;
        var remoteDuration = Math.Abs(durationMinutes2 % 60) + 10;
        var offset = (aosOffsetMinutes % 30);

        var noradId = "25544";
        var localPass = CreatePass(noradId, baseTime, TimeSpan.FromMinutes(localDuration));
        var remotePass = CreatePass(noradId, baseTime.AddMinutes(offset), TimeSpan.FromMinutes(remoteDuration));

        var localPasses = new List<PassInfo> { localPass };
        var remotePasses = new List<PassInfo> { remotePass };

        var results = MutualPassFinder.FindOverlaps(localPasses, remotePasses, TimeSpan.FromMinutes(1));

        return results.All(r =>
        {
            var expectedStart = r.LocalPass.AosUtc > r.RemotePass.AosUtc
                ? r.LocalPass.AosUtc
                : r.RemotePass.AosUtc;
            var expectedEnd = r.LocalPass.LosUtc < r.RemotePass.LosUtc
                ? r.LocalPass.LosUtc
                : r.RemotePass.LosUtc;

            return r.MutualStartUtc == expectedStart && r.MutualEndUtc == expectedEnd;
        });
    }

    /// <summary>
    /// Property 12: Mutual Pass Duration Threshold.
    ///
    /// For any returned MutualPassInfo with minimum duration D,
    /// (MutualEndUtc - MutualStartUtc) SHALL be >= D.
    /// </summary>
    [Property]
    public bool Overlap_duration_meets_minimum_threshold(int aosOffsetMinutes, int durationMinutes1, int durationMinutes2, int minDurationMinutes)
    {
        if (!IsFinite(aosOffsetMinutes) || !IsFinite(durationMinutes1) || !IsFinite(durationMinutes2) || !IsFinite(minDurationMinutes))
            return true;

        var baseTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var localDuration = Math.Abs(durationMinutes1 % 60) + 10;
        var remoteDuration = Math.Abs(durationMinutes2 % 60) + 10;
        var offset = (aosOffsetMinutes % 30);
        var minDuration = TimeSpan.FromMinutes(Math.Abs(minDurationMinutes % 15) + 1); // 1-15 minutes

        var noradId = "25544";
        var localPass = CreatePass(noradId, baseTime, TimeSpan.FromMinutes(localDuration));
        var remotePass = CreatePass(noradId, baseTime.AddMinutes(offset), TimeSpan.FromMinutes(remoteDuration));

        var localPasses = new List<PassInfo> { localPass };
        var remotePasses = new List<PassInfo> { remotePass };

        var results = MutualPassFinder.FindOverlaps(localPasses, remotePasses, minDuration);

        return results.All(r => (r.MutualEndUtc - r.MutualStartUtc) >= minDuration);
    }

    /// <summary>
    /// Property 13: Mutual Pass Sorted Output.
    ///
    /// For any input pass lists, the result of FindOverlaps SHALL be sorted
    /// by MutualStartUtc in ascending order.
    /// </summary>
    [Property]
    public bool Results_are_sorted_by_mutual_start_utc(int offset1, int offset2, int offset3, int dur1, int dur2, int dur3)
    {
        if (!IsFinite(offset1) || !IsFinite(offset2) || !IsFinite(offset3) ||
            !IsFinite(dur1) || !IsFinite(dur2) || !IsFinite(dur3))
            return true;

        var baseTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var noradId = "25544";

        // Create multiple local passes at different times
        var localPasses = new List<PassInfo>
        {
            CreatePass(noradId, baseTime.AddMinutes(Math.Abs(offset1 % 120)), TimeSpan.FromMinutes(Math.Abs(dur1 % 30) + 10)),
            CreatePass(noradId, baseTime.AddMinutes(Math.Abs(offset2 % 120) + 120), TimeSpan.FromMinutes(Math.Abs(dur2 % 30) + 10)),
            CreatePass(noradId, baseTime.AddMinutes(Math.Abs(offset3 % 120) + 240), TimeSpan.FromMinutes(Math.Abs(dur3 % 30) + 10))
        };

        // Remote passes that overlap with each local pass
        var remotePasses = new List<PassInfo>
        {
            CreatePass(noradId, baseTime.AddMinutes(Math.Abs(offset1 % 120) + 2), TimeSpan.FromMinutes(Math.Abs(dur1 % 30) + 10)),
            CreatePass(noradId, baseTime.AddMinutes(Math.Abs(offset2 % 120) + 122), TimeSpan.FromMinutes(Math.Abs(dur2 % 30) + 10)),
            CreatePass(noradId, baseTime.AddMinutes(Math.Abs(offset3 % 120) + 242), TimeSpan.FromMinutes(Math.Abs(dur3 % 30) + 10))
        };

        var results = MutualPassFinder.FindOverlaps(localPasses, remotePasses, TimeSpan.FromMinutes(1));

        for (int i = 1; i < results.Count; i++)
        {
            if (results[i].MutualStartUtc < results[i - 1].MutualStartUtc)
                return false;
        }

        return true;
    }

    private static PassInfo CreatePass(string noradId, DateTime aos, TimeSpan duration)
    {
        return new PassInfo
        {
            SatelliteName = $"SAT-{noradId}",
            NoradId = noradId,
            AosUtc = aos,
            LosUtc = aos + duration,
            MaxElevationDeg = 45.0,
            MaxElevationUtc = aos + duration / 2,
            AosAzimuthDeg = 180.0,
            LosAzimuthDeg = 0.0
        };
    }

    private static bool IsFinite(int value) => value != int.MinValue;
}
