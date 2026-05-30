using OscarWatch.Core.Models;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

public sealed class IcomSerialTransportTests
{
    [Fact]
    public void TryExtractLastFrame_returns_last_complete_frame()
    {
        var buffer = new byte[]
        {
            0xFE, 0xFE, 0x60, 0x00, 0xFA, 0xFD,
            0xFE, 0xFE, 0x60, 0x00, 0xFB, 0xFD
        };

        Assert.True(IcomSerialTransport.TryExtractLastFrame(buffer, out var frame));
        Assert.Equal(
            [0xFE, 0xFE, 0x60, 0x00, 0xFB, 0xFD],
            frame);
    }

    [Fact]
    public void TryExtractLastFrame_returns_false_for_incomplete_frame()
    {
        Assert.False(IcomSerialTransport.TryExtractLastFrame([0xFE, 0xFE, 0x60], out _));
    }
}
