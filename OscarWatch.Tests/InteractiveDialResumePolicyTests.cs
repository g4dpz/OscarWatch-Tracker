using OscarWatch.Core.Radio;

namespace OscarWatch.Tests;

public sealed class InteractiveDialResumePolicyTests
{
    [Fact]
    public void ResolveSettleMs_uses_default_when_unset()
    {
        Assert.Equal(InteractiveDialResumePolicy.DefaultSettleMs, InteractiveDialResumePolicy.ResolveSettleMs(0));
        Assert.Equal(InteractiveDialResumePolicy.DefaultSettleMs, InteractiveDialResumePolicy.ResolveSettleMs(-1));
    }

    [Fact]
    public void ResolveSettleMs_clamps_to_range()
    {
        Assert.Equal(InteractiveDialResumePolicy.MinSettleMs, InteractiveDialResumePolicy.ResolveSettleMs(50));
        Assert.Equal(InteractiveDialResumePolicy.MaxSettleMs, InteractiveDialResumePolicy.ResolveSettleMs(20_000));
        Assert.Equal(1500, InteractiveDialResumePolicy.ResolveSettleMs(1500));
    }

    [Fact]
    public void ResolveUplinkResumeMs_uses_default_when_unset()
    {
        Assert.Equal(
            InteractiveDialResumePolicy.DefaultUplinkResumeMs,
            InteractiveDialResumePolicy.ResolveUplinkResumeMs(0));
        Assert.Equal(4000, InteractiveDialResumePolicy.ResolveUplinkResumeMs(4000));
        Assert.Equal(
            InteractiveDialResumePolicy.MinUplinkResumeMs,
            InteractiveDialResumePolicy.ResolveUplinkResumeMs(100));
        Assert.Equal(
            InteractiveDialResumePolicy.MaxUplinkResumeMs,
            InteractiveDialResumePolicy.ResolveUplinkResumeMs(50_000));
    }

    [Fact]
    public void IsDialSettled_false_until_timer_elapses()
    {
        var start = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);
        Assert.False(InteractiveDialResumePolicy.IsDialSettled(DateTime.MinValue, start, 800, identicalSampleCount: 1));
        Assert.False(InteractiveDialResumePolicy.IsDialSettled(start, start.AddMilliseconds(799), 800, identicalSampleCount: 1));
        Assert.True(InteractiveDialResumePolicy.IsDialSettled(start, start.AddMilliseconds(800), 800, identicalSampleCount: 1));
        Assert.True(InteractiveDialResumePolicy.IsDialSettled(start, start.AddMilliseconds(400), 200, identicalSampleCount: 1));
        Assert.False(InteractiveDialResumePolicy.IsDialSettled(start, start.AddMilliseconds(400), 0, identicalSampleCount: 1));
        Assert.True(InteractiveDialResumePolicy.IsDialSettled(start, start.AddMilliseconds(800), 0, identicalSampleCount: 1));
    }

    [Fact]
    public void IsDialSettled_eight_identical_samples_match_default_800_ms()
    {
        var start = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);
        Assert.False(InteractiveDialResumePolicy.IsDialSettled(start, start, 800, identicalSampleCount: 7));
        Assert.True(InteractiveDialResumePolicy.IsDialSettled(start, start, 800, identicalSampleCount: 8));
        Assert.True(InteractiveDialResumePolicy.IsDialSettled(start, start, 200, identicalSampleCount: 2));
        Assert.False(InteractiveDialResumePolicy.IsDialSettled(start, start, 2000, identicalSampleCount: 8));
        Assert.True(InteractiveDialResumePolicy.IsDialSettled(start, start, 2000, identicalSampleCount: 20));
    }
}
