using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public sealed class SatelliteDatabaseMergePlan
{
    public required List<SatelliteDatabaseNewSatellite> NewSatellites { get; init; }
    public required List<SatelliteDatabaseNewMode> NewModes { get; init; }
    public required List<SatelliteDatabaseMergeConflict> Conflicts { get; init; }

    public bool HasChanges =>
        NewSatellites.Count > 0 || NewModes.Count > 0 || Conflicts.Count > 0;
}

public sealed class SatelliteDatabaseNewSatellite
{
    public required SatelliteRadioEntry Entry { get; init; }

    public string Key => Entry.Name.Trim();
}

public sealed class SatelliteDatabaseNewMode
{
    public required string SatelliteName { get; init; }
    public required SatelliteTransponderMode Mode { get; init; }

    public string Key => SatelliteDatabaseMerger.ModeKey(SatelliteName, Mode.Type);
}

public sealed class SatelliteDatabaseMergeConflict
{
    public required string SatelliteName { get; init; }
    public required string ModeType { get; init; }
    public required SatelliteTransponderMode LocalMode { get; init; }
    public required SatelliteTransponderMode RemoteMode { get; init; }

    public string Key => SatelliteDatabaseMerger.ModeKey(SatelliteName, ModeType);
}

public sealed class SatelliteDatabaseMergeSelection
{
    public HashSet<string> AcceptedNewSatelliteKeys { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> AcceptedNewModeKeys { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Conflict keys where the remote mode should replace the local mode.</summary>
    public HashSet<string> AcceptRemoteConflictKeys { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Conflict keys where the user confirms keeping the local mode.</summary>
    public HashSet<string> AcceptLocalConflictKeys { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
