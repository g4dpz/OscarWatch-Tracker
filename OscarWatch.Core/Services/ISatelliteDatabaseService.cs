using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public interface ISatelliteDatabaseService
{
    IReadOnlyList<SatelliteRadioEntry> Entries { get; }

    SatelliteRadioEntry? TryGetEntry(string satelliteName);
}
