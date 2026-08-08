using OscarWatch.Core.Recording;

namespace OscarWatch.Tests;

public sealed class FfmpegLocatorTests
{
    [Fact]
    public async Task ProbeAsync_marks_unavailable_when_process_fails()
    {
        var runner = new FakeExternalProcessRunner
        {
            NextResult = new ExternalProcessResult(-1, "", "not found", TimedOut: false)
        };
        var locator = new FfmpegLocator(runner);

        var result = await locator.ProbeAsync();

        Assert.False(result.IsAvailable);
        Assert.Null(result.ExecutablePath);
        Assert.Contains("not found", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProbeAsync_marks_available_on_successful_version()
    {
        var runner = new FakeExternalProcessRunner
        {
            NextResult = new ExternalProcessResult(0, "ffmpeg version 6.0", "", TimedOut: false)
        };
        var locator = new FfmpegLocator(runner);

        var result = await locator.ProbeAsync();

        Assert.True(result.IsAvailable);
        Assert.Equal("ffmpeg", result.ExecutablePath);
        Assert.Null(result.Detail);
    }

    [Fact]
    public async Task ProbeAsync_caches_result_until_forced_refresh()
    {
        var runner = new FakeExternalProcessRunner
        {
            NextResult = new ExternalProcessResult(0, "ffmpeg version 6.0", "", TimedOut: false)
        };
        var locator = new FfmpegLocator(runner);

        Assert.True((await locator.ProbeAsync()).IsAvailable);
        Assert.Equal(1, runner.CallCount);

        runner.NextResult = new ExternalProcessResult(-1, "", "gone", TimedOut: false);
        Assert.True((await locator.ProbeAsync()).IsAvailable);
        Assert.Equal(1, runner.CallCount);

        Assert.False((await locator.ProbeAsync(forceRefresh: true)).IsAvailable);
        Assert.Equal(2, runner.CallCount);
    }
}

public sealed class FfmpegMp3ConverterTests
{
    [Fact]
    public void BuildArguments_uses_lame_vbr_quality()
    {
        var args = FfmpegMp3Converter.BuildArguments(@"C:\in.wav", @"C:\out.mp3");
        Assert.Equal(
            ["-hide_banner", "-loglevel", "error", "-y", "-i", @"C:\in.wav",
                "-codec:a", "libmp3lame", "-qscale:a", "2", @"C:\out.mp3"],
            args);
    }

    [Fact]
    public void EstimateTimeout_scales_with_file_size()
    {
        var small = FfmpegMp3Converter.EstimateTimeout(1024);
        var large = FfmpegMp3Converter.EstimateTimeout(100L * 1024 * 1024);
        Assert.True(large > small);
        Assert.True(large <= TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task ConvertWavToMp3Async_succeeds_and_deletes_wav()
    {
        var dir = Path.Combine(Path.GetTempPath(), "oscarwatch-ffmpeg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var wavPath = Path.Combine(dir, "clip.wav");
        var mp3Path = Path.Combine(dir, "clip.mp3");
        await File.WriteAllBytesAsync(wavPath, [1, 2, 3, 4]);

        var runner = new FakeExternalProcessRunner
        {
            NextResult = new ExternalProcessResult(0, "", "", TimedOut: false),
            OnRun = (_, _) => File.WriteAllBytes(mp3Path, [9, 9, 9])
        };
        var locator = new FfmpegLocator(runner);
        // Prime locator cache as available without consuming the convert run.
        runner.NextResult = new ExternalProcessResult(0, "ffmpeg version", "", TimedOut: false);
        Assert.True((await locator.ProbeAsync()).IsAvailable);

        runner.NextResult = new ExternalProcessResult(0, "", "", TimedOut: false);
        var converter = new FfmpegMp3Converter(runner, locator);
        var result = await converter.ConvertWavToMp3Async(wavPath);

        Assert.True(result.Success);
        Assert.Equal(mp3Path, result.OutputPath);
        Assert.False(File.Exists(wavPath));
        Assert.True(File.Exists(mp3Path));

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public async Task ConvertWavToMp3Async_keeps_wav_when_ffmpeg_fails()
    {
        var dir = Path.Combine(Path.GetTempPath(), "oscarwatch-ffmpeg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var wavPath = Path.Combine(dir, "clip.wav");
        await File.WriteAllBytesAsync(wavPath, [1, 2, 3, 4]);

        var runner = new FakeExternalProcessRunner
        {
            NextResult = new ExternalProcessResult(0, "ffmpeg version", "", TimedOut: false)
        };
        var locator = new FfmpegLocator(runner);
        Assert.True((await locator.ProbeAsync()).IsAvailable);

        runner.NextResult = new ExternalProcessResult(1, "", "encode failed", TimedOut: false);
        var converter = new FfmpegMp3Converter(runner, locator);
        var result = await converter.ConvertWavToMp3Async(wavPath);

        Assert.False(result.Success);
        Assert.True(File.Exists(wavPath));
        Assert.Contains("encode failed", result.Error, StringComparison.OrdinalIgnoreCase);

        Directory.Delete(dir, recursive: true);
    }
}

internal sealed class FakeExternalProcessRunner : IExternalProcessRunner
{
    public ExternalProcessResult NextResult { get; set; } =
        new(0, "", "", TimedOut: false);

    public Action<string, IReadOnlyList<string>>? OnRun { get; set; }
    public int CallCount { get; private set; }

    public Task<ExternalProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        OnRun?.Invoke(fileName, arguments);
        return Task.FromResult(NextResult);
    }
}
