using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

public sealed class RigStatusTextTests
{
    [Theory]
    [InlineData(RigStatusKind.Tracking, null, null, "Tracking")]
    [InlineData(RigStatusKind.CatPaused, null, null, "CAT paused (manual tuning)")]
    [InlineData(RigStatusKind.NotConnected, "COM3", "Access denied", "Rig not connected (COM3): Access denied")]
    [InlineData(RigStatusKind.DualNotConnected, null, "downlink offline", "Dual radio not connected: downlink offline")]
    [InlineData(RigStatusKind.SerialPortNotFound, "/dev/ttyUSB1", null, "Serial port not found (/dev/ttyUSB1). Check the USB cable and refresh the port list.")]
    [InlineData(RigStatusKind.SerialPortBusy, "COM3", null, "Serial port in use (COM3). Close other CAT programs or choose a different port.")]
    [InlineData(RigStatusKind.DualRadioSamePort, "/dev/ttyUSB0", null, "Downlink and uplink radios both use /dev/ttyUSB0. Use different COM ports for each radio.")]
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
