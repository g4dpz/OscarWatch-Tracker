using System.Net.Http.Headers;
using OscarWatch.Core.Net;

namespace OscarWatch.Tests;

public class OscarWatchHttpClientsTests
{
    [Fact]
    public void Create_sets_user_agent_product_and_version()
    {
        using var client = OscarWatchHttpClients.Create(TimeSpan.FromSeconds(5));

        var ua = Assert.Single(client.DefaultRequestHeaders.UserAgent);
        Assert.Equal(OscarWatchHttpClients.ProductName, ua.Product?.Name);
        Assert.False(string.IsNullOrWhiteSpace(ua.Product?.Version));
    }

    [Fact]
    public void ApplyUserAgent_does_not_duplicate_existing_header()
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("TestClient", "9.9"));

        OscarWatchHttpClients.ApplyUserAgent(client);

        Assert.Single(client.DefaultRequestHeaders.UserAgent);
        Assert.Equal("TestClient", client.DefaultRequestHeaders.UserAgent.First().Product?.Name);
    }
}
