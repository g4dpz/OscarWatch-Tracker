using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public interface ILiveTrackerSnapshotProvider
{
    LiveTrackerSnapshot GetCurrent();
}
