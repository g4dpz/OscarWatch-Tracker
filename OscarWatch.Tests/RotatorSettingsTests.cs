using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

public sealed class RotatorSettingsTests
{
    [Fact]
    public void UsesNetworkEndpoint_true_for_urc()
    {
        var settings = new RotatorSettings
        {
            Type = RotatorType.UrcTcp,
            TransportKind = RotatorTransportKind.Serial
        };
        Assert.True(settings.UsesNetworkEndpoint);
        Assert.False(settings.UsesSerialPort);
    }

    [Fact]
    public void UsesNetworkEndpoint_true_for_tcp_serial_transport()
    {
        var settings = new RotatorSettings
        {
            Type = RotatorType.YaesuGs232,
            TransportKind = RotatorTransportKind.Tcp,
            NetworkHost = "192.168.1.20",
            NetworkPort = 4001
        };
        Assert.True(settings.UsesNetworkEndpoint);
        Assert.False(settings.UsesSerialPort);
        Assert.True(settings.HasConfiguredEndpoint);
    }

    [Fact]
    public void HasConfiguredEndpoint_requires_host_for_tcp_serial()
    {
        var settings = new RotatorSettings
        {
            Type = RotatorType.EasyComm,
            TransportKind = RotatorTransportKind.Tcp,
            NetworkHost = "",
            NetworkPort = 4001,
            Port = "COM3"
        };
        Assert.False(settings.HasConfiguredEndpoint);

        settings.NetworkHost = "127.0.0.1";
        Assert.True(settings.HasConfiguredEndpoint);
    }

    [Fact]
    public void UsesSerialPort_when_serial_transport()
    {
        var settings = new RotatorSettings
        {
            Type = RotatorType.Spid,
            TransportKind = RotatorTransportKind.Serial,
            Port = "COM5"
        };
        Assert.True(settings.UsesSerialPort);
        Assert.False(settings.UsesNetworkEndpoint);
        Assert.True(settings.HasConfiguredEndpoint);
    }
}
