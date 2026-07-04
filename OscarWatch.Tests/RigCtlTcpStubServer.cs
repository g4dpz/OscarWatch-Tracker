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

    public RigCtlTcpStubServer()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        FrequencyHz = 145_960_000;
        _serverTask = Task.Run(RunServerAsync);
    }

    public int Port { get; }

    public long FrequencyHz { get; private set; }

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
            TcpClient? client = null;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false);
                await HandleClientAsync(client).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
            }
            finally
            {
                client?.Dispose();
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client)
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
}
