using OscarWatch.Core.Services;
using Serilog;

namespace OscarWatch.Recording;

public sealed class LoggingRecordingTaskScheduler : IRecordingTaskScheduler
{
    private static readonly ILogger Log = Serilog.Log.ForContext<LoggingRecordingTaskScheduler>();

    public void Schedule(Func<Task> task, string description)
    {
        _ = RunAsync(task, description);
    }

    private static async Task RunAsync(Func<Task> task, string description)
    {
        try
        {
            await task().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Log.Debug("Recording task cancelled ({Description})", description);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Recording task failed ({Description})", description);
        }
    }
}
