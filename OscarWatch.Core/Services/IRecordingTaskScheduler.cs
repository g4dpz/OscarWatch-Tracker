namespace OscarWatch.Core.Services;

/// <summary>Runs recording start/stop tasks without blocking callers; implementations may log failures.</summary>
public interface IRecordingTaskScheduler
{
    void Schedule(Func<Task> task, string description);
}
