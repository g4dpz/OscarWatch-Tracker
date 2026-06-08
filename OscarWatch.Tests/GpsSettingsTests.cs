using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

public sealed class GpsSettingsTests
{
    [Fact]
    public void IsConfigured_requires_serial_port_for_serial_connection()
    {
        var settings = new GpsSettings { Enabled = true, ConnectionKind = GpsConnectionKind.Serial };
        Assert.False(settings.IsConfigured);

        settings.Port = "COM9";
        Assert.True(settings.IsConfigured);
    }

    [Fact]
    public void IsConfigured_requires_gpsd_host_for_network_connection()
    {
        var settings = new GpsSettings
        {
            Enabled = true,
            ConnectionKind = GpsConnectionKind.Gpsd,
            GpsdHost = "",
            GpsdPort = GpsSettings.DefaultGpsdPort
        };
        Assert.False(settings.IsConfigured);

        settings.GpsdHost = "192.168.1.10";
        Assert.True(settings.IsConfigured);
        Assert.False(settings.UsesSerialPort);
    }
}
