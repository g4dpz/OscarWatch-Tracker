namespace OscarWatch.Core.Services;

public sealed class DefaultRecordingTaskScheduler : IRecordingTaskScheduler
{
    public static DefaultRecordingTaskScheduler Instance { get; } = new();

    public void Schedule(Func<Task> task, string description) => _ = task();
}
