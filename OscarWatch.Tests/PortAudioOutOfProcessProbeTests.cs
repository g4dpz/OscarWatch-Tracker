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
    public void TryRun_succeeds_when_probe_is_present()
    {
        var ok = PortAudioOutOfProcessProbe.TryRun(out var error);

        Assert.True(ok, error);
        Assert.Null(error);
    }
}
