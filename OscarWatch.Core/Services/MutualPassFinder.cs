using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public static class MutualPassFinder
{
    public static IReadOnlyList<MutualPassInfo> FindOverlaps(
        IReadOnlyList<PassInfo> localPasses,
        IReadOnlyList<PassInfo> remotePasses,
        TimeSpan minimumMutualDuration)
    {
        var remoteByNorad = remotePasses
            .GroupBy(p => p.NoradId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var results = new List<MutualPassInfo>();

        foreach (var local in localPasses)
        {
            if (!remoteByNorad.TryGetValue(local.NoradId, out var remoteList))
                continue;

            foreach (var remote in remoteList)
            {
                var start = local.AosUtc > remote.AosUtc ? local.AosUtc : remote.AosUtc;
                var end = local.LosUtc < remote.LosUtc ? local.LosUtc : remote.LosUtc;
                var duration = end - start;
                if (duration < minimumMutualDuration)
                    continue;

                results.Add(new MutualPassInfo
                {
                    SatelliteName = local.SatelliteName,
                    NoradId = local.NoradId,
                    MutualStartUtc = start,
                    MutualEndUtc = end,
                    LocalPass = local,
                    RemotePass = remote
                });
            }
        }

        return results
            .OrderBy(p => p.MutualStartUtc)
            .ToList();
    }
}
