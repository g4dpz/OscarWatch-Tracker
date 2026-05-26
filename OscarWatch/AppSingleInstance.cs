using System.IO.Pipes;
using System.Text;
using Serilog;

namespace OscarWatch;

/// <summary>
/// Ensures only one OscarWatch process runs per user session.
/// A second launch notifies the running instance to restore its main window.
/// </summary>
internal static class AppSingleInstance
{
    private const string MutexName = "OscarWatch.SingleInstance.Mutex";
    private const string PipeName = "OscarWatch.SingleInstance.Pipe";
    private const string ActivateMessage = "activate";

    public static bool AllowsMultipleInstances(string[] args) =>
        args.Any(a => string.Equals(a, "--allow-multiple", StringComparison.OrdinalIgnoreCase));

    public static bool TryBecomePrimaryInstance(out IDisposable? holder)
    {
        var createdNew = false;
        Mutex mutex;

        try
        {
            mutex = new Mutex(initiallyOwned: true, MutexName, out createdNew);
        }
        catch (AbandonedMutexException ex)
        {
            Log.Warning(ex, "Recovered abandoned single-instance mutex");
            mutex = ex.Mutex ?? new Mutex(initiallyOwned: true, MutexName);
            createdNew = true;
        }

        if (!createdNew)
        {
            mutex.Dispose();
            holder = null;
            return false;
        }

        holder = new PrimaryInstanceHolder(mutex);
        return true;
    }

    public static bool NotifyPrimaryInstance()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (TrySendActivateMessage())
                return true;

            Thread.Sleep(200);
        }

        return false;
    }

    public static void StartActivationListener(Action activate)
    {
        _activate = activate;
        _serverCts = new CancellationTokenSource();
        _ = Task.Run(() => RunActivationServerAsync(_serverCts.Token));
    }

    private static Action? _activate;
    private static CancellationTokenSource? _serverCts;

    private static bool TrySendActivateMessage()
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.Out,
                PipeOptions.None);

            client.Connect(1500);
            var bytes = Encoding.UTF8.GetBytes(ActivateMessage);
            client.Write(bytes);
            client.Flush();
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static async Task RunActivationServerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var server = new NamedPipeServerStream(
                PipeName,
                PipeDirection.In,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                var buffer = new byte[ActivateMessage.Length];
                var read = 0;
                while (read < buffer.Length && server.CanRead)
                {
                    var chunk = await server.ReadAsync(buffer.AsMemory(read), cancellationToken).ConfigureAwait(false);
                    if (chunk == 0)
                        break;

                    read += chunk;
                }

                if (read == buffer.Length
                    && Encoding.UTF8.GetString(buffer).Equals(ActivateMessage, StringComparison.Ordinal))
                {
                    _activate?.Invoke();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Single-instance activation pipe error");
            }
        }
    }

    private sealed class PrimaryInstanceHolder : IDisposable
    {
        private readonly Mutex _mutex;
        private bool _disposed;

        public PrimaryInstanceHolder(Mutex mutex) => _mutex = mutex;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _serverCts?.Cancel();
            _serverCts?.Dispose();
            _serverCts = null;
            _activate = null;

            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Not owned on this thread after abandon recovery.
            }

            _mutex.Dispose();
        }
    }
}
