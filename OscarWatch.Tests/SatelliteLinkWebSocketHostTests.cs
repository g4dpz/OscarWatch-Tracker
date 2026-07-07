using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using OscarWatch.Core.Models;
using OscarWatch.SatelliteLink;

namespace OscarWatch.Tests;

public class SatelliteLinkWebSocketHostTests
{
    [Fact]
    public async Task Client_receives_snapshot_on_connect_and_after_broadcast()
    {
        var port = GetFreePort();
        var settings = new SatelliteLinkSettings
        {
            Enabled = true,
            Port = port,
            AllowLanClients = false
        };

        await using var host = new SatelliteLinkWebSocketHost();
        await host.StartAsync(settings);

        using var client = new ClientWebSocket();
        await client.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/"), CancellationToken.None);

        var payload = """{"type":"satelliteStatus","version":1,"inRange":false,"wispDde":"** NO SATELLITE **"}""";
        await host.BroadcastAsync(payload);

        var received = await ReceiveTextAsync(client);
        using var doc = JsonDocument.Parse(received);
        Assert.Equal("satelliteStatus", doc.RootElement.GetProperty("type").GetString());

        var update = """{"type":"satelliteStatus","version":1,"inRange":true,"satellite":{"name":"SO-50"}}""";
        await host.BroadcastAsync(update);
        received = await ReceiveTextAsync(client);
        Assert.Contains("SO-50", received, StringComparison.Ordinal);
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task<string> ReceiveTextAsync(ClientWebSocket client)
    {
        var buffer = new byte[4096];
        var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }
}
