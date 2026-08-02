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

    [Fact]
    public void Radios_returns_cached_list_until_change()
    {
        using var service = new FlexDiscoveryService();
        service.IngestDatagram(Encoding.ASCII.GetBytes(
            "serial=C-1 model=FLEX-6600 nickname=Bravo ip=10.0.0.3 port=4992"));
        service.IngestDatagram(Encoding.ASCII.GetBytes(
            "serial=A-1 model=FLEX-6700 nickname=Alpha ip=10.0.0.1 port=4992"));

        // First access builds the sorted cache
        var first = service.Radios;
        Assert.Equal(2, first.Count);
        Assert.Equal("Alpha", first[0].Nickname); // sorted by nickname

        // Second access returns the same cached reference (no re-sort)
        var second = service.Radios;
        Assert.Same(first, second);

        // Duplicate datagram (no change) — cache not invalidated
        service.IngestDatagram(Encoding.ASCII.GetBytes(
            "serial=A-1 model=FLEX-6700 nickname=Alpha ip=10.0.0.1 port=4992"));
        var third = service.Radios;
        Assert.Same(first, third);

        // Actual change invalidates the cache
        service.IngestDatagram(Encoding.ASCII.GetBytes(
            "serial=A-1 model=FLEX-6700 nickname=Zulu ip=10.0.0.1 port=4992"));
        var fourth = service.Radios;
        Assert.NotSame(first, fourth);
        Assert.Equal("Bravo", fourth[0].Nickname); // re-sorted: Bravo < Zulu
        Assert.Equal("Zulu", fourth[1].Nickname);
    }

    [Fact]
    public void Clear_invalidates_cached_radios()
    {
        using var service = new FlexDiscoveryService();
        service.IngestDatagram(Encoding.ASCII.GetBytes(
            "serial=X-1 model=FLEX-6600 nickname=Test ip=10.0.0.1 port=4992"));

        var before = service.Radios;
        Assert.Single(before);

        service.Clear();
        var after = service.Radios;
        Assert.Empty(after);
        Assert.NotSame(before, after);
    }
}
