using System.Diagnostics;
using System.Text;

namespace OscarWatch.Core.Recording;

public sealed class ProcessExternalProcessRunner : IExternalProcessRunner
{
    public async Task<ExternalProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stderr.AppendLine(e.Data);
        };

        try
        {
            if (!process.Start())
            {
                return new ExternalProcessResult(
                    ExitCode: -1,
                    StandardOutput: "",
                    StandardError: "Failed to start process.",
                    TimedOut: false);
            }
        }
        catch (Exception ex)
        {
            return new ExternalProcessResult(
                ExitCode: -1,
                StandardOutput: "",
                StandardError: ex.Message,
                TimedOut: false);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            try
            {
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }

            return new ExternalProcessResult(
                ExitCode: -1,
                StandardOutput: stdout.ToString(),
                StandardError: AppendTimeout(stderr.ToString()),
                TimedOut: true);
        }

        return new ExternalProcessResult(
            process.ExitCode,
            stdout.ToString(),
            stderr.ToString(),
            TimedOut: false);
    }

    private static string AppendTimeout(string stderr) =>
        string.IsNullOrWhiteSpace(stderr)
            ? "Process timed out."
            : stderr.TrimEnd() + Environment.NewLine + "Process timed out.";

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // ignore
        }
    }
}
