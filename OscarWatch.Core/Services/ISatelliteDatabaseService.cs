using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public interface ISatelliteDatabaseService
{
    IReadOnlyList<SatelliteRadioEntry> Entries { get; }

    string ActiveDatabasePath { get; }

    bool IsUsingUserDatabase { get; }

    SatelliteRadioEntry? TryGetEntry(string satelliteName);

    void Reload();
}
