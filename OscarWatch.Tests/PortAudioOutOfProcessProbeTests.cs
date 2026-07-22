using OscarWatch.Recording;

namespace OscarWatch.Tests;

public sealed class PortAudioOutOfProcessProbeTests
{
    [Fact]
    public void ResolveProbePath_finds_probe_next_to_test_output()
    {
        var path = PortAudioOutOfProcessProbe.ResolveProbePath();
        Assert.NotNull(path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void TryRun_reports_success_or_soft_failure_without_throwing()
    {
        // CI runners often lack PortAudio native deps (e.g. libjack). The probe must still
        // return a clear result instead of crashing the host process.
        var ok = PortAudioOutOfProcessProbe.TryRun(out var error);

        if (ok)
        {
            Assert.Null(error);
            return;
        }

        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.DoesNotContain("probe executable was not found", error, StringComparison.OrdinalIgnoreCase);
    }
}
