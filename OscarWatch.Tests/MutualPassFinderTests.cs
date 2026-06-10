using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 4.5, 4.6**
///
/// Edge-case tests for <see cref="MutualPassFinder"/> verifying correct behaviour
/// when no common NORAD IDs exist and when overlap is shorter than the minimum duration.
/// </summary>
public sealed class MutualPassFinderTests
{
    /// <summary>
    /// Requirement 4.5: WHEN no passes share a common NORAD ID,
    /// THE MutualPassFinder SHALL return an empty list.
    /// </summary>
    [Fact]
    public void No_common_norad_id_returns_empty_list()
    {
        var baseTime = new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Utc);

        var localPasses = new List<PassInfo>
        {
            CreatePass("25544", baseTime, TimeSpan.FromMinutes(15))
        };

        var remotePasses = new List<PassInfo>
        {
            CreatePass("99999", baseTime, TimeSpan.FromMinutes(15))
        };

        var results = MutualPassFinder.FindOverlaps(localPasses, remotePasses, TimeSpan.FromMinutes(1));

        Assert.Empty(results);
    }

    /// <summary>
    /// Requirement 4.6: WHEN overlapping passes exist but the overlap duration
    /// is less than the minimum, THE MutualPassFinder SHALL exclude those passes from results.
    /// </summary>
    [Fact]
    public void Overlap_shorter_than_minimum_duration_is_excluded()
    {
        var baseTime = new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Utc);
        var minimumDuration = TimeSpan.FromMinutes(5);

        // Local pass: 10:00 – 10:12
        var localPass = CreatePass("25544", baseTime, TimeSpan.FromMinutes(12));

        // Remote pass: 10:10 – 10:25 → overlap is 10:10–10:12 = 2 minutes (less than 5 min threshold)
        var remotePass = CreatePass("25544", baseTime.AddMinutes(10), TimeSpan.FromMinutes(15));

        var localPasses = new List<PassInfo> { localPass };
        var remotePasses = new List<PassInfo> { remotePass };

        var results = MutualPassFinder.FindOverlaps(localPasses, remotePasses, minimumDuration);

        Assert.Empty(results);
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
}
