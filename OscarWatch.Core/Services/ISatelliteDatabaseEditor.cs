using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public interface ISatelliteDatabaseEditor
{
    string BundledPath { get; }
    string UserPath { get; }
    string ActivePath { get; }
    bool IsUsingUserDatabase { get; }

    List<SatelliteRadioEntry> LoadForEditing();
    void Save(IReadOnlyList<SatelliteRadioEntry> entries);
    void ResetToBundled();
}
