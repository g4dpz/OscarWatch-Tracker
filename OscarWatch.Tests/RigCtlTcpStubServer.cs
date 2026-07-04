using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OscarWatch.Tests;

internal sealed class RigCtlTcpStubServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _serverTask;
    private readonly TaskCompletionSource _acceptLoopReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _frequencyHz;

    public RigCtlTcpStubServer()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _frequencyHz = 145_960_000;
        _serverTask = Task.Run(RunServerAsync);
    }

    public int Port { get; }

    public long FrequencyHz
    {
        get => Interlocked.Read(ref _frequencyHz);
        private set => Interlocked.Exchange(ref _frequencyHz, value);
    }

    public void WaitUntilReady(TimeSpan? timeout = null)
    {
        if (!_acceptLoopReady.Task.Wait(timeout ?? TimeSpan.FromSeconds(5)))
            throw new TimeoutException("RigCtl TCP stub did not start accepting connections in time.");
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        try
        {
            _serverTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
        }

        _cts.Dispose();
    }

    private async Task RunServerAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                _acceptLoopReady.TrySetResult();
                var client = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false);
                _ = HandleClientAsync(client);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            await using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { AutoFlush = true };

            while (client.Connected && !_cts.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(_cts.Token).ConfigureAwait(false);
                if (line is null)
                    break;

                if (line.StartsWith("F ", StringComparison.Ordinal))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2
                        && long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hz))
                        FrequencyHz = hz;
                    await writer.WriteLineAsync("RPRT 0").ConfigureAwait(false);
                    continue;
                }

                if (string.Equals(line, "f", StringComparison.Ordinal))
                {
                    await writer.WriteLineAsync(FrequencyHz.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
                    await writer.WriteLineAsync("RPRT 0").ConfigureAwait(false);
                    continue;
                }

                if (line.StartsWith("M ", StringComparison.Ordinal))
                    await writer.WriteLineAsync("RPRT 0").ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            client.Dispose();
        }
    }
}
