using System.Text;
using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

public class FlexDiscoveryServiceTests
{
    private const string SampleAscii =
        "discovery_protocol_version=2.0.0.0 model=FLEX-6600 serial=ABC-6600-1 " +
        "nickname=Lab ip=192.168.10.5 port=4992 status=Available";

    [Fact]
    public void IngestDatagram_AddsAndDedupesBySerial()
    {
        using var service = new FlexDiscoveryService();
        var bytes = Encoding.ASCII.GetBytes(SampleAscii);

        Assert.True(service.IngestDatagram(bytes));
        Assert.Single(service.Radios);
        Assert.Equal("192.168.10.5", service.Radios[0].IpAddress);

        Assert.False(service.IngestDatagram(bytes));
        Assert.Single(service.Radios);

        var updated = Encoding.ASCII.GetBytes(
            "model=FLEX-6600 serial=ABC-6600-1 nickname=Lab2 ip=192.168.10.6 port=4992");
        Assert.True(service.IngestDatagram(updated));
        Assert.Single(service.Radios);
        Assert.Equal("192.168.10.6", service.Radios[0].IpAddress);
        Assert.Equal("Lab2", service.Radios[0].Nickname);
    }

    [Fact]
    public void IngestDatagram_MultipleSerials()
    {
        using var service = new FlexDiscoveryService();
        Assert.True(service.IngestDatagram(Encoding.ASCII.GetBytes(
            "serial=A-1 model=FLEX-6600 ip=10.0.0.1 port=4992")));
        Assert.True(service.IngestDatagram(Encoding.ASCII.GetBytes(
            "serial=B-2 model=FLEX-6700 ip=10.0.0.2 port=4992")));
        Assert.Equal(2, service.Radios.Count);
    }

    [Fact]
    public void IngestDatagram_InvalidPayload_ReturnsFalse()
    {
        using var service = new FlexDiscoveryService();
        Assert.False(service.IngestDatagram(Encoding.ASCII.GetBytes("not a discovery packet")));
        Assert.Empty(service.Radios);
    }
}
