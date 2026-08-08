namespace OscarWatch.Core.Recording;

public sealed record FfmpegConvertResult(bool Success, string? OutputPath, string? Error);

public sealed class FfmpegMp3Converter
{
    private static readonly TimeSpan MinTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MaxTimeout = TimeSpan.FromMinutes(15);
    private const long BytesPerExtraMinute = 20L * 1024 * 1024;

    private readonly IExternalProcessRunner _runner;
    private readonly FfmpegLocator _locator;

    public FfmpegMp3Converter(IExternalProcessRunner? runner = null, FfmpegLocator? locator = null)
    {
        _runner = runner ?? new ProcessExternalProcessRunner();
        _locator = locator ?? new FfmpegLocator(_runner);
    }

    public static IReadOnlyList<string> BuildArguments(string wavPath, string mp3Path) =>
    [
        "-hide_banner",
        "-loglevel",
        "error",
        "-y",
        "-i",
        wavPath,
        "-codec:a",
        "libmp3lame",
        "-qscale:a",
        "2",
        mp3Path
    ];

    public static TimeSpan EstimateTimeout(long wavBytes)
    {
        if (wavBytes <= 0)
            return MinTimeout;

        var extraMinutes = wavBytes / BytesPerExtraMinute;
        var timeout = MinTimeout + TimeSpan.FromMinutes(extraMinutes);
        return timeout > MaxTimeout ? MaxTimeout : timeout;
    }

    public async Task<FfmpegConvertResult> ConvertWavToMp3Async(
        string wavPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(wavPath))
            return new FfmpegConvertResult(false, null, "WAV path is empty.");

        if (!File.Exists(wavPath))
            return new FfmpegConvertResult(false, null, "WAV file was not found.");

        var probe = await _locator.ProbeAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!probe.IsAvailable || string.IsNullOrWhiteSpace(probe.ExecutablePath))
        {
            return new FfmpegConvertResult(
                false,
                null,
                probe.Detail ?? "ffmpeg was not found on PATH.");
        }

        var mp3Path = Path.ChangeExtension(wavPath, ".mp3");
        if (string.IsNullOrWhiteSpace(mp3Path) ||
            string.Equals(mp3Path, wavPath, StringComparison.OrdinalIgnoreCase))
        {
            return new FfmpegConvertResult(false, null, "Could not derive MP3 output path.");
        }

        long wavBytes;
        try
        {
            wavBytes = new FileInfo(wavPath).Length;
        }
        catch (Exception ex)
        {
            return new FfmpegConvertResult(false, null, ex.Message);
        }

        var result = await _runner.RunAsync(
            probe.ExecutablePath,
            BuildArguments(wavPath, mp3Path),
            EstimateTimeout(wavBytes),
            cancellationToken).ConfigureAwait(false);

        if (result.TimedOut)
        {
            TryDelete(mp3Path);
            return new FfmpegConvertResult(false, null, "ffmpeg timed out while converting to MP3.");
        }

        if (result.ExitCode != 0)
        {
            TryDelete(mp3Path);
            var detail = string.IsNullOrWhiteSpace(result.StandardError)
                ? $"ffmpeg exited with code {result.ExitCode}."
                : result.StandardError.Trim();
            return new FfmpegConvertResult(false, null, detail);
        }

        try
        {
            var info = new FileInfo(mp3Path);
            if (!info.Exists || info.Length <= 0)
            {
                TryDelete(mp3Path);
                return new FfmpegConvertResult(false, null, "ffmpeg did not produce an MP3 file.");
            }
        }
        catch (Exception ex)
        {
            TryDelete(mp3Path);
            return new FfmpegConvertResult(false, null, ex.Message);
        }

        if (!TryDelete(wavPath))
        {
            return new FfmpegConvertResult(
                true,
                mp3Path,
                "MP3 created, but the intermediate WAV could not be deleted.");
        }

        return new FfmpegConvertResult(true, mp3Path, null);
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
