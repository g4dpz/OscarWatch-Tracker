using System.IO;
using OscarWatch.Core.Hardware;

namespace OscarWatch.Tests;

public sealed class SerialPortConnectErrorHelperTests
{
    [Fact]
    public void Classify_port_not_found_from_linux_inner_io_exception()
    {
        var ex = new UnauthorizedAccessException(
            "Access to the port '/dev/ttyUSB1' is denied.",
            new IOException("No such file or directory : '/dev/ttyUSB1'"));

        Assert.Equal(SerialPortConnectErrorKind.PortNotFound, SerialPortConnectErrorHelper.Classify(ex));
    }

    [Fact]
    public void Classify_port_busy_from_linux_inner_io_exception()
    {
        var ex = new UnauthorizedAccessException(
            "Access to the port '/dev/ttyUSB0' is denied.",
            new IOException("Device or resource busy"));

        Assert.Equal(SerialPortConnectErrorKind.PortBusy, SerialPortConnectErrorHelper.Classify(ex));
    }

    [Fact]
    public void Classify_port_busy_from_windows_in_use_message()
    {
        var ex = new UnauthorizedAccessException("Access to the port 'COM3' is denied.");
        Assert.Equal(SerialPortConnectErrorKind.PortBusy, SerialPortConnectErrorHelper.Classify(ex));
    }

    [Fact]
    public void TryDescribeDualSamePort_detects_matching_ports()
    {
        Assert.True(SerialPortConnectErrorHelper.TryDescribeDualSamePort("/dev/ttyUSB0", "/dev/ttyUSB0", out var port));
        Assert.Equal("/dev/ttyUSB0", port);
    }

    [Fact]
    public void TryDescribeDualSamePort_ignores_dummy_uplink_empty_port()
    {
        Assert.False(SerialPortConnectErrorHelper.TryDescribeDualSamePort("COM3", "", out _));
    }

    [Theory]
    [InlineData(SerialPortConnectErrorKind.PortNotFound, "/dev/ttyUSB1", null)]
    [InlineData(SerialPortConnectErrorKind.PortBusy, "COM3", null)]
    [InlineData(SerialPortConnectErrorKind.DualSamePort, "/dev/ttyUSB0", null)]
    public void ToEnglish_includes_port_and_guidance(
        SerialPortConnectErrorKind kind,
        string port,
        string? endpoint)
    {
        var text = SerialPortConnectErrorHelper.ToEnglish(kind, port, endpoint);
        Assert.Contains(port, text, StringComparison.Ordinal);
    }

    [Fact]
    public void ToEnglish_includes_endpoint_for_dual_leg_failures()
    {
        var text = SerialPortConnectErrorHelper.ToEnglish(
            SerialPortConnectErrorKind.PortNotFound,
            "/dev/ttyUSB2",
            SerialPortConnectErrorHelper.EndpointDownlink);

        Assert.Contains("Downlink", text, StringComparison.Ordinal);
        Assert.Contains("/dev/ttyUSB2", text, StringComparison.Ordinal);
    }
}
