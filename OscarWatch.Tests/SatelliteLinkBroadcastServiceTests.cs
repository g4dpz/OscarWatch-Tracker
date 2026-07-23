using OscarWatch.Core.Models;
using OscarWatch.SatelliteLink;

namespace OscarWatch.Tests;

public class SatelliteLinkBroadcastServiceTests
{
    [Fact]
    public async Task ApplySettings_ConcurrentCalls_EndListeningWithoutPortConflict()
    {
        var port = GetFreePort();
        var settings = new SatelliteLinkSettings
        {
            Enabled = true,
            Port = port,
            AllowLanClients = false
        };

        var service = new SatelliteLinkBroadcastService();
        try
        {
            Parallel.For(0, 24, _ => service.ApplySettings(settings));

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline && !service.IsListening)
                await Task.Delay(50);

            Assert.True(service.IsListening);
            Assert.True(string.IsNullOrWhiteSpace(service.LastError));
        }
        finally
        {
            await service.StopAsync();
        }
    }

    [Fact]
    public async Task ApplySettings_RapidReload_StaysListeningOnSamePort()
    {
        var port = GetFreePort();
        var settings = new SatelliteLinkSettings
        {
            Enabled = true,
            Port = port,
            AllowLanClients = false
        };

        var service = new SatelliteLinkBroadcastService();
        try
        {
            service.ApplySettings(settings);
            await WaitUntilListeningAsync(service);

            for (var i = 0; i < 10; i++)
                service.ApplySettings(settings);

            await WaitUntilListeningAsync(service);
            Assert.True(string.IsNullOrWhiteSpace(service.LastError));
        }
        finally
        {
            await service.StopAsync();
        }
    }

    private static async Task WaitUntilListeningAsync(SatelliteLinkBroadcastService service)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && !service.IsListening)
            await Task.Delay(50);

        Assert.True(service.IsListening);
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
