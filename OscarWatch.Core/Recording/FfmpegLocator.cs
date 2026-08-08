namespace OscarWatch.Core.Recording;

public sealed record FfmpegProbeResult(bool IsAvailable, string? ExecutablePath, string? Detail);

public sealed class FfmpegLocator
{
    public const string DefaultExecutableName = "ffmpeg";
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private readonly IExternalProcessRunner _runner;
    private readonly string _executableName;
    private readonly object _gate = new();
    private FfmpegProbeResult? _cached;

    public FfmpegLocator(IExternalProcessRunner? runner = null, string executableName = DefaultExecutableName)
    {
        _runner = runner ?? new ProcessExternalProcessRunner();
        _executableName = string.IsNullOrWhiteSpace(executableName)
            ? DefaultExecutableName
            : executableName.Trim();
    }

    public FfmpegProbeResult Probe(bool forceRefresh = false) =>
        ProbeAsync(forceRefresh).GetAwaiter().GetResult();

    public async Task<FfmpegProbeResult> ProbeAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (!forceRefresh)
        {
            lock (_gate)
            {
                if (_cached is not null)
                    return _cached;
            }
        }

        var result = await ProbeCoreAsync(cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            if (!forceRefresh && _cached is not null)
                return _cached;

            _cached = result;
            return _cached;
        }
    }

    private async Task<FfmpegProbeResult> ProbeCoreAsync(CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(
            _executableName,
            ["-version"],
            ProbeTimeout,
            cancellationToken).ConfigureAwait(false);

        if (result.TimedOut)
        {
            return new FfmpegProbeResult(
                false,
                null,
                "ffmpeg probe timed out.");
        }

        if (result.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(result.StandardError)
                ? "ffmpeg was not found on PATH."
                : result.StandardError.Trim();
            return new FfmpegProbeResult(false, null, detail);
        }

        return new FfmpegProbeResult(true, _executableName, null);
    }
}
