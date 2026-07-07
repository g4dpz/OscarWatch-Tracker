using OscarWatch.Core.Models;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

public sealed class RigCtlTcpClientTests
{
    private const int TestCommandTimeoutMs = 2000;

    [Fact]
    public void Set_and_read_frequency_round_trip()
    {
        using var server = new RigCtlTcpStubServer();
        server.WaitUntilReady();
        using var client = new RigCtlTcpClient("127.0.0.1", server.Port, TestCommandTimeoutMs);
        client.Open();

        Assert.True(client.SetFrequencyHz(435_800_000));
        Assert.Equal(435_800_000, client.ReadFrequencyHz());
        Assert.Equal(435_800_000, server.FrequencyHz);
    }

    [Fact]
    public void Set_mode_accepts_hamlib_response()
    {
        using var server = new RigCtlTcpStubServer();
        server.WaitUntilReady();
        using var client = new RigCtlTcpClient("127.0.0.1", server.Port, TestCommandTimeoutMs);
        client.Open();

        Assert.True(client.SetMode("USB"));
    }

    [Fact]
    public void RigCtlTcpDriver_implements_read_and_write()
    {
        using var server = new RigCtlTcpStubServer();
        server.WaitUntilReady();
        using var driver = new RigCtlTcpDriver("127.0.0.1", server.Port, TestCommandTimeoutMs);
        driver.Open();

        Assert.True(driver.SetFrequencyHz(145_920_000));
        Assert.Equal(145_920_000, driver.ReadFrequencyHz(RigVfo.Main));
    }

    [Fact]
    public void RigDriverFactory_creates_sdr_downlink_driver()
    {
        using var server = new RigCtlTcpStubServer();
        server.WaitUntilReady();
        using var driver = RigDriverFactory.Create(new RigEndpointSettings
        {
            Type = RigType.SdrRigCtlTcp,
            NetworkHost = "127.0.0.1",
            NetworkPort = server.Port,
            CatDelayMs = TestCommandTimeoutMs
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
