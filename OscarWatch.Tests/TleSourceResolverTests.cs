using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

public sealed class TleSourceResolverTests
{
    [Fact]
    public void TryGetNetworkUrl_returns_default_for_oscarwatch()
    {
        var url = TleSourceResolver.TryGetNetworkUrl(new TleSourceSettings());
        Assert.Equal(TleService.DefaultTleUrl, url);
    }

    [Fact]
    public void TryGetNetworkUrl_returns_amsat_nasabare()
    {
        var url = TleSourceResolver.TryGetNetworkUrl(new TleSourceSettings
        {
            Mode = TleSourceMode.AmsatOrg
        });
        Assert.Equal(TleSourceResolver.AmsatNasabareUrl, url);
    }

    [Fact]
    public void TryGetNetworkUrl_returns_custom_url()
    {
        var url = TleSourceResolver.TryGetNetworkUrl(new TleSourceSettings
        {
            Mode = TleSourceMode.CustomUrl,
            CustomUrl = "https://example.com/tle.txt"
        });
        Assert.Equal("https://example.com/tle.txt", url);
    }

    [Fact]
    public void UsesNetwork_is_false_for_local_file()
    {
        Assert.False(TleSourceResolver.UsesNetwork(new TleSourceSettings
        {
            Mode = TleSourceMode.LocalFile,
            LocalFilePath = @"C:\tle.txt"
        }));
    }
}
