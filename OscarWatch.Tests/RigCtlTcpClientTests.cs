using OscarWatch.Core.Models;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

public sealed class RigCtlTcpClientTests : IDisposable
{
    private readonly RigCtlTcpStubServer _server = new();

    public void Dispose() => _server.Dispose();

    [Fact]
    public void Set_and_read_frequency_round_trip()
    {
        using var client = new RigCtlTcpClient("127.0.0.1", _server.Port);
        client.Open();

        Assert.True(client.SetFrequencyHz(435_800_000));
        Assert.Equal(435_800_000, client.ReadFrequencyHz());
        Assert.Equal(435_800_000, _server.FrequencyHz);
    }

    [Fact]
    public void Set_mode_accepts_hamlib_response()
    {
        using var client = new RigCtlTcpClient("127.0.0.1", _server.Port);
        client.Open();

        Assert.True(client.SetMode("USB"));
    }

    [Fact]
    public void RigCtlTcpDriver_implements_read_and_write()
    {
        using var driver = new RigCtlTcpDriver("127.0.0.1", _server.Port);
        driver.Open();

        Assert.True(driver.SetFrequencyHz(145_920_000));
        Assert.Equal(145_920_000, driver.ReadFrequencyHz(RigVfo.Main));
    }

    [Fact]
    public void RigDriverFactory_creates_sdr_downlink_driver()
    {
        var driver = RigDriverFactory.Create(new RigEndpointSettings
        {
            Type = RigType.SdrRigCtlTcp,
            NetworkHost = "127.0.0.1",
            NetworkPort = _server.Port,
            CatDelayMs = 0
        });

        Assert.IsType<RigCtlTcpDriver>(driver);
        driver.Open();
        Assert.True(driver.IsConnected);
    }

    [Fact]
    public void Sdr_endpoint_is_configured_without_com_port()
    {
        var endpoint = new RigEndpointSettings
        {
            Type = RigType.SdrRigCtlTcp,
            NetworkHost = "127.0.0.1",
            NetworkPort = 4532
        };

        Assert.True(endpoint.IsConfigured);
    }
}
