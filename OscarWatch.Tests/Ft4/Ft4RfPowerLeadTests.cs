using System.Diagnostics;
using OscarWatch.Core.Ft4;

namespace OscarWatch.Tests.Ft4;

[Collection(Ft4ClockCollection.Name)]
public sealed class Ft4RfPowerLeadTests
{
    [Fact]
    public void Read_starts_in_the_quiet_tail_before_the_slot()
    {
        var slot = new DateTime(2026, 10, 6, 12, 0, 7, 500, DateTimeKind.Utc);
        Assert.Equal(slot - Ft4RfPowerLead.Lead, Ft4RfPowerLead.CheckAtUtc(slot));
        Assert.Equal(TimeSpan.FromSeconds(1.5), Ft4RfPowerLead.Lead);
    }

    [Fact]
    public void Scheduler_waking_inside_the_lead_window_starts_the_read_at_once()
    {
        var slot = DateTime.UtcNow.AddMilliseconds(100);
        var sw = Stopwatch.StartNew();
        Ft4SlotWait.UntilUtc(Ft4RfPowerLead.CheckAtUtc(slot));
        Assert.True(sw.ElapsedMilliseconds < 20, $"waited {sw.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Unanswered_read_lets_the_slot_transmit()
    {
        var read = new TaskCompletionSource<bool>();
        var sw = Stopwatch.StartNew();
        Assert.True(Ft4RfPowerLead.AwaitVerdict(read.Task));
        Assert.True(sw.ElapsedMilliseconds < 50, $"waited {sw.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Passing_verdict_allows_the_slot()
    {
        Assert.True(Ft4RfPowerLead.AwaitVerdict(Task.FromResult(true)));
    }

    [Fact]
    public void Over_limit_verdict_blocks_the_slot()
    {
        Assert.False(Ft4RfPowerLead.AwaitVerdict(Task.FromResult(false)));
    }

    [Fact]
    public void Failed_read_lets_the_slot_transmit()
    {
        var failed = Task.FromException<bool>(new InvalidOperationException("cat"));
        Assert.True(Ft4RfPowerLead.AwaitVerdict(failed));
    }
}
