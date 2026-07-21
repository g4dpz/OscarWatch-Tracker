using Serilog;

namespace OscarWatch.Services;

internal static class AppShutdownWatchdog
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);
    private static readonly object Gate = new();
    private static CancellationTokenSource? _cts;

    public static void Start()
    {
        lock (Gate)
        {
            if (_cts is not null)
                return;

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            Log.Information(
                "Shutdown watchdog started for process {ProcessId}; timeout={TimeoutSeconds}s",
                Environment.ProcessId,
                Timeout.TotalSeconds);

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(Timeout, token).ConfigureAwait(false);
                    Log.Fatal(
                        "OscarWatch shutdown exceeded {TimeoutSeconds}s; forcing process {ProcessId} to exit",
                        Timeout.TotalSeconds,
                        Environment.ProcessId);
                    Environment.Exit(2);
                }
                catch (OperationCanceledException)
                {
                }
            }, CancellationToken.None);
        }
    }

    public static void Cancel()
    {
        lock (Gate)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
