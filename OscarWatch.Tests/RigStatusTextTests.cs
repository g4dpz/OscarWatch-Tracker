using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

public sealed class RigStatusTextTests
{
    [Theory]
    [InlineData(RigStatusKind.Tracking, null, null, "Tracking")]
    [InlineData(RigStatusKind.CatPaused, null, null, "CAT paused (manual tuning)")]
    [InlineData(RigStatusKind.NotConnected, "COM3", "Access denied", "Rig not connected (COM3): Access denied")]
    [InlineData(RigStatusKind.DualNotConnected, null, "downlink offline", "Dual radio not connected: downlink offline")]
    public void ToEnglish_formats_known_statuses(
        RigStatusKind kind,
        string? port,
        string? detail,
        string expected)
    {
        var status = new RigConnectionStatus(false, false, kind, port, detail, null, null);
        Assert.Equal(expected, RigStatusText.ToEnglish(status));
    }
}
