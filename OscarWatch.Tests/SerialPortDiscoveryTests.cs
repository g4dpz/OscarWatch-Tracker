using OscarWatch.Rotator;

namespace OscarWatch.Tests;

public sealed class SerialPortPathFormatterTests
{
    [Theory]
    [InlineData("/dev/serial/by-id/usb-Vendor_Model_Serial-if00-port0", "by-id: usb-Vendor_Model_Serial-if00-port0")]
    [InlineData("/dev/serial/by-path/pci-0000:00:14.0-usb-0:1:1.0-port0", "by-path: pci-0000:00:14.0-usb-0:1:1.0-port0")]
    [InlineData("/dev/USB821H", "USB821H")]
    [InlineData("/dev/ttyICR9K", "ttyICR9K")]
    [InlineData("/dev/ttyUSB0", "/dev/ttyUSB0")]
    [InlineData("COM3", "COM3")]
    public void FormatDisplay_formats_known_path_shapes(string path, string expected)
    {
        Assert.Equal(expected, SerialPortPathFormatter.FormatDisplay(path));
    }
}

public sealed class SerialPortCatalogTests
{
    private static readonly Dictionary<string, string> DualRadioDeviceMap = new(StringComparer.Ordinal)
    {
        ["/dev/ttyUSB0"] = "/dev/ttyUSB0",
        ["/dev/USB821H"] = "/dev/ttyUSB0",
        ["/dev/serial/by-id/usb-Radio_Serial"] = "/dev/ttyUSB0",
        ["/dev/ttyUSB1"] = "/dev/ttyUSB1",
        ["/dev/serial/by-id/usb-Radio_A"] = "/dev/ttyUSB0",
        ["/dev/serial/by-id/usb-Radio_B"] = "/dev/ttyUSB1",
    };

    private static string? ResolveMappedDevice(string path) =>
        DualRadioDeviceMap.TryGetValue(path, out var resolved) ? resolved : path;

    [Fact]
    public void BuildDisplayList_prefers_udev_alias_over_kernel_name_for_same_device()
    {
        var display = SerialPortCatalog.BuildDisplayList(
            ["/dev/ttyUSB0"],
            ["/dev/USB821H", "/dev/serial/by-id/usb-Radio_Serial"],
            ResolveMappedDevice);

        Assert.Single(display);
        Assert.Equal("/dev/USB821H", display[0]);
    }

    [Fact]
    public void BuildDisplayList_prefers_by_id_when_no_custom_alias_exists()
    {
        var display = SerialPortCatalog.BuildDisplayList(
            ["/dev/ttyUSB0"],
            ["/dev/serial/by-id/usb-Radio_Serial"],
            path => path switch
            {
                "/dev/ttyUSB0" => "/dev/ttyUSB0",
                "/dev/serial/by-id/usb-Radio_Serial" => "/dev/ttyUSB0",
                _ => path
            });

        Assert.Single(display);
        Assert.Equal("/dev/serial/by-id/usb-Radio_Serial", display[0]);
    }

    [Fact]
    public void BuildDisplayList_keeps_distinct_devices_separate()
    {
        var display = SerialPortCatalog.BuildDisplayList(
            ["/dev/ttyUSB0", "/dev/ttyUSB1"],
            ["/dev/serial/by-id/usb-Radio_A", "/dev/serial/by-id/usb-Radio_B"],
            ResolveMappedDevice);

        Assert.Equal(2, display.Count);
        Assert.Contains("/dev/serial/by-id/usb-Radio_A", display);
        Assert.Contains("/dev/serial/by-id/usb-Radio_B", display);
    }

    [Fact]
    public void BuildDisplayList_prefers_tty_prefixed_udev_alias_over_kernel_name()
    {
        var display = SerialPortCatalog.BuildDisplayList(
            ["/dev/ttyUSB1"],
            ["/dev/ttyICR9K", "/dev/serial/by-id/usb-Icom"],
            path => path switch
            {
                "/dev/ttyUSB1" => "/dev/ttyUSB1",
                "/dev/ttyICR9K" => "/dev/ttyUSB1",
                "/dev/serial/by-id/usb-Icom" => "/dev/ttyUSB1",
                _ => path
            });

        Assert.Single(display);
        Assert.Equal("/dev/ttyICR9K", display[0]);
    }

    [Fact]
    public void BuildDisplayList_includes_windows_ports_unchanged()
    {
        var display = SerialPortCatalog.BuildDisplayList(["COM3", "COM7"], []);

        Assert.Equal(["COM3", "COM7"], display);
    }

    [Theory]
    [InlineData("/dev/USB821H", 0)]
    [InlineData("/dev/ttyICR9K", 0)]
    [InlineData("/dev/ttyFT736", 0)]
    [InlineData("/dev/serial/by-id/usb-Radio", 1)]
    [InlineData("/dev/serial/by-path/pci-usb-port0", 2)]
    [InlineData("/dev/ttyUSB0", 3)]
    [InlineData("/dev/ttyACM0", 3)]
    [InlineData("/dev/ttyS0", 3)]
    [InlineData("COM3", 3)]
    public void GetPathPriority_orders_stable_paths_before_kernel_names(string path, int expected)
    {
        Assert.Equal(expected, SerialPortCatalog.GetPathPriority(path));
    }

    [Theory]
    [InlineData("ttyUSB0", true)]
    [InlineData("ttyACM1", true)]
    [InlineData("ttyAMA0", true)]
    [InlineData("ttyS0", true)]
    [InlineData("tty0", true)]
    [InlineData("tty12", true)]
    [InlineData("ttySAC0", true)]
    [InlineData("ttyICR9K", false)]
    [InlineData("ttyFT736", false)]
    [InlineData("ttyPCR1K", false)]
    [InlineData("ttyTMV71", false)]
    [InlineData("ttyTS870", false)]
    [InlineData("USB821H", false)]
    public void IsKernelTtyDeviceName_distinguishes_kernel_nodes_from_ham_aliases(string name, bool expected)
    {
        Assert.Equal(expected, SerialPortCatalog.IsKernelTtyDeviceName(name));
    }

    [Theory]
    [InlineData("ttyICR9K", true)]
    [InlineData("USB821H", true)]
    [InlineData("ic910-civ", true)]
    [InlineData("ttyUSB0", false)]
    [InlineData("tty0", false)]
    [InlineData("loop0", false)]
    public void IsLikelyCustomUdevAliasName_accepts_ham_aliases(string name, bool expected)
    {
        Assert.Equal(expected, SerialPortCatalog.IsLikelyCustomUdevAliasName(name));
    }
}
