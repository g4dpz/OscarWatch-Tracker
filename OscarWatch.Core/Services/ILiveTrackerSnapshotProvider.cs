using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public interface ILiveTrackerSnapshotProvider
{
    string? FocusedNoradId { get; }

    LiveTrackerSnapshot GetCurrent();
}
