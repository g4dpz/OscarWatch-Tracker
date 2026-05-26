using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public static class SatelliteDatabaseMerger
{
    public static string ModeKey(string satelliteName, string modeType) =>
        $"{satelliteName.Trim()}|{modeType.Trim()}";

    public static SatelliteDatabaseMergePlan BuildPlan(
        IReadOnlyList<SatelliteRadioEntry> localEntries,
        IReadOnlyList<SatelliteRadioEntry> remoteEntries)
    {
        var localByName = IndexByName(localEntries);
        var newSatellites = new List<SatelliteDatabaseNewSatellite>();
        var newModes = new List<SatelliteDatabaseNewMode>();
        var conflicts = new List<SatelliteDatabaseMergeConflict>();

        foreach (var remoteEntry in remoteEntries)
        {
            if (string.IsNullOrWhiteSpace(remoteEntry.Name))
                continue;

            var remoteName = remoteEntry.Name.Trim();
            var normalizedRemote = CloneEntry(SatelliteDatabaseFile.NormalizeEntry(remoteEntry));

            if (!localByName.TryGetValue(remoteName, out var localEntry))
            {
                newSatellites.Add(new SatelliteDatabaseNewSatellite { Entry = normalizedRemote });
                continue;
            }

            var localModesByType = IndexModesByType(localEntry);
            foreach (var remoteMode in normalizedRemote.Modes)
            {
                if (string.IsNullOrWhiteSpace(remoteMode.Type))
                    continue;

                var typeKey = remoteMode.Type.Trim();
                if (!localModesByType.TryGetValue(typeKey, out var localMode))
                {
                    newModes.Add(new SatelliteDatabaseNewMode
                    {
                        SatelliteName = remoteName,
                        Mode = CloneMode(remoteMode)
                    });
                    continue;
                }

                if (!ModesEqual(localMode, remoteMode))
                {
                    conflicts.Add(new SatelliteDatabaseMergeConflict
                    {
                        SatelliteName = remoteName,
                        ModeType = typeKey,
                        LocalMode = CloneMode(localMode),
                        RemoteMode = CloneMode(remoteMode)
                    });
                }
            }
        }

        return new SatelliteDatabaseMergePlan
        {
            NewSatellites = newSatellites,
            NewModes = newModes,
            Conflicts = conflicts
        };
    }

    public static List<SatelliteRadioEntry> Apply(
        IReadOnlyList<SatelliteRadioEntry> localEntries,
        SatelliteDatabaseMergePlan plan,
        SatelliteDatabaseMergeSelection selection)
    {
        var result = localEntries.Select(CloneEntry).ToList();
        var byName = IndexByName(result);

        foreach (var addition in plan.NewSatellites)
        {
            if (!selection.AcceptedNewSatelliteKeys.Contains(addition.Key))
                continue;

            if (byName.ContainsKey(addition.Key))
                continue;

            var clone = CloneEntry(addition.Entry);
            result.Add(clone);
            byName[addition.Key] = clone;
        }

        foreach (var addition in plan.NewModes)
        {
            if (!selection.AcceptedNewModeKeys.Contains(addition.Key))
                continue;

            if (!byName.TryGetValue(addition.SatelliteName.Trim(), out var entry))
                continue;

            var modesByType = IndexModesByType(entry);
            if (modesByType.ContainsKey(addition.Mode.Type.Trim()))
                continue;

            entry.Modes.Add(CloneMode(addition.Mode));
        }

        foreach (var conflict in plan.Conflicts)
        {
            if (!selection.AcceptRemoteConflictKeys.Contains(conflict.Key))
                continue;

            if (!byName.TryGetValue(conflict.SatelliteName.Trim(), out var entry))
                continue;

            for (var i = 0; i < entry.Modes.Count; i++)
            {
                if (!string.Equals(entry.Modes[i].Type.Trim(), conflict.ModeType.Trim(), StringComparison.OrdinalIgnoreCase))
                    continue;

                entry.Modes[i] = CloneMode(conflict.RemoteMode);
                break;
            }
        }

        return result
            .Select(SatelliteDatabaseFile.NormalizeEntry)
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool ModesEqual(SatelliteTransponderMode left, SatelliteTransponderMode right) =>
        NearlyEqual(left.DownlinkKHz, right.DownlinkKHz)
        && NearlyEqual(left.UplinkKHz, right.UplinkKHz)
        && string.Equals(left.DownlinkMode.Trim(), right.DownlinkMode.Trim(), StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.UplinkMode.Trim(), right.UplinkMode.Trim(), StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.Doppler.Trim(), right.Doppler.Trim(), StringComparison.OrdinalIgnoreCase)
        && NearlyEqual(left.CtcssHz, right.CtcssHz)
        && NearlyEqual(left.CtcssArmHz, right.CtcssArmHz);

    public static string DescribeMode(SatelliteTransponderMode mode)
    {
        var rx = mode.DownlinkKHz;
        var tx = mode.UplinkKHz;
        var tone = mode.CtcssHz is > 0 ? $", CTCSS {mode.CtcssHz:0.#}" : "";
        return tx <= 0
            ? $"RX {rx:0.###} kHz · {mode.DownlinkMode}{tone}"
            : $"TX {tx:0.###} ↑ · RX {rx:0.###} ↓ · {mode.UplinkMode}/{mode.DownlinkMode} · {mode.Doppler}{tone}";
    }

    private static Dictionary<string, SatelliteRadioEntry> IndexByName(IReadOnlyList<SatelliteRadioEntry> entries)
    {
        var map = new Dictionary<string, SatelliteRadioEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
                continue;

            map[entry.Name.Trim()] = entry;
        }

        return map;
    }

    private static Dictionary<string, SatelliteTransponderMode> IndexModesByType(SatelliteRadioEntry entry)
    {
        var map = new Dictionary<string, SatelliteTransponderMode>(StringComparer.OrdinalIgnoreCase);
        foreach (var mode in entry.Modes)
        {
            if (string.IsNullOrWhiteSpace(mode.Type))
                continue;

            map[mode.Type.Trim()] = mode;
        }

        return map;
    }

    private static bool NearlyEqual(double? left, double? right)
    {
        if (left is null && right is null)
            return true;

        if (left is null || right is null)
            return false;

        return Math.Abs(left.Value - right.Value) < 0.0001;
    }

    private static bool NearlyEqual(double left, double right) =>
        Math.Abs(left - right) < 0.0001;

    private static SatelliteRadioEntry CloneEntry(SatelliteRadioEntry source) =>
        new()
        {
            Name = source.Name,
            Modes = source.Modes.Select(CloneMode).ToList()
        };

    private static SatelliteTransponderMode CloneMode(SatelliteTransponderMode source) =>
        new()
        {
            Type = source.Type,
            DownlinkKHz = source.DownlinkKHz,
            UplinkKHz = source.UplinkKHz,
            DownlinkMode = source.DownlinkMode,
            UplinkMode = source.UplinkMode,
            Doppler = source.Doppler,
            CtcssHz = source.CtcssHz,
            CtcssArmHz = source.CtcssArmHz
        };
}
