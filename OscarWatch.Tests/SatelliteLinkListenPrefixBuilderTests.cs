using OscarWatch.Core.Models;
using OscarWatch.Core.SatelliteLink;

namespace OscarWatch.Tests;

public class SatelliteLinkListenPrefixBuilderTests
{
    [Fact]
    public void Build_local_only_uses_loopback_prefix()
    {
        var prefixes = SatelliteLinkListenPrefixBuilder.Build(new SatelliteLinkSettings
        {
            Port = 7373,
            AllowLanClients = false
        });

        Assert.Single(prefixes);
        Assert.Equal("http://127.0.0.1:7373/", prefixes[0]);
    }

    [Fact]
    public void Build_lan_includes_loopback_and_does_not_use_wildcard_plus()
    {
        var prefixes = SatelliteLinkListenPrefixBuilder.Build(new SatelliteLinkSettings
        {
            Port = 7373,
            AllowLanClients = true
        });

        Assert.Contains("http://127.0.0.1:7373/", prefixes);
        Assert.DoesNotContain(prefixes, p => p.Contains("http://+:", StringComparison.Ordinal));
        Assert.DoesNotContain(prefixes, p => p.Contains("http://*:", StringComparison.Ordinal));
    }

    [Fact]
    public void FormatEndpointPreview_lists_ws_urls()
    {
        var preview = SatelliteLinkListenPrefixBuilder.FormatEndpointPreview(new SatelliteLinkSettings
        {
            Port = 7373,
            AllowLanClients = false
        });

        Assert.Equal("ws://127.0.0.1:7373/", preview);
    }

    [Fact]
    public void DescribeBindFailure_maps_port_in_use()
    {
        var message = SatelliteLinkListenPrefixBuilder.DescribeBindFailure(
            new InvalidOperationException("Only one usage of each socket address (protocol/network address/port) is normally permitted"));

        Assert.Contains("already in use", message, StringComparison.OrdinalIgnoreCase);
    }
}
