using OscarWatch.Core.Ft4;

namespace OscarWatch.Tests.Ft4;

public sealed class Ft4SlotClockTests
{
    [Fact]
    public void SlotStartUtc_aligns_to_7_5_seconds()
    {
        var utc = new DateTime(2026, 9, 21, 12, 0, 8, 200, DateTimeKind.Utc);
        var start = Ft4SlotClock.SlotStartUtc(utc, Ft4SlotClock.Ft4SlotSeconds);
        Assert.Equal(new DateTime(2026, 9, 21, 12, 0, 7, 500, DateTimeKind.Utc), start);
    }

    [Fact]
    public void IsEvenSlot_alternates()
    {
        var even = new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);
        var odd = new DateTime(2026, 9, 21, 12, 0, 7, 500, DateTimeKind.Utc);
        Assert.True(Ft4SlotClock.IsEvenSlot(even, Ft4SlotClock.Ft4SlotSeconds));
        Assert.False(Ft4SlotClock.IsEvenSlot(odd, Ft4SlotClock.Ft4SlotSeconds));
    }

    [Fact]
    public void Early_decode_window_is_after_burst_and_before_slot_end()
    {
        Assert.True(Ft4SlotClock.Ft4EarlyDecodeSeconds > Ft4SlotClock.Ft4SymbolBurstSeconds);
        Assert.True(Ft4SlotClock.Ft4EarlyDecodeSeconds < Ft4SlotClock.Ft4SlotSeconds);
    }
}
