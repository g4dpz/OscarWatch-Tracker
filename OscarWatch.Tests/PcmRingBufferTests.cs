using OscarWatch.Recording;

namespace OscarWatch.Tests;

public sealed class PcmRingBufferTests
{
    [Fact]
    public void TryWrite_Read_RoundTrip()
    {
        var ring = new PcmRingBuffer(16);
        ring.TryWrite([1, 2, 3, 4]);

        var buffer = new byte[4];
        Assert.Equal(4, ring.Read(buffer));
        Assert.Equal([1, 2, 3, 4], buffer);
        Assert.Equal(0, ring.Count);
    }

    [Fact]
    public void TryWrite_WhenFull_RecordsDroppedBytes()
    {
        var ring = new PcmRingBuffer(4);
        ring.TryWrite([1, 2, 3, 4]);
        ring.TryWrite([5, 6]);

        Assert.Equal(2, ring.DroppedBytes);
        Assert.Equal(4, ring.Count);
    }
}
