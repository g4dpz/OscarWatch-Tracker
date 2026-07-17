using OscarWatch.Core.Models;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

public sealed class IcomIc905DriverTests
{
    [Fact]
    public void SelectVfo_Main_uses_vfo_a_civ_selector()
    {
        var transport = new RecordingIcomCivTransport();
        var driver = new IcomIc905Driver(transport);
        driver.Open();
        transport.SentCommandBodies.Clear();

        driver.SelectVfo(RigVfo.Main, force: true);

        Assert.Contains(transport.SentCommandBodies, body => body == "0700");
    }

    [Fact]
    public void SetSatelliteMode_is_no_op()
    {
        var transport = new RecordingIcomCivTransport();
        var driver = new IcomIc905Driver(transport);
        driver.Open();
        var countBefore = transport.CommandCount;

        driver.SetSatelliteMode(true);
        driver.SetSatelliteMode(false);

        Assert.Equal(countBefore, transport.CommandCount);
    }

    [Fact]
    public void DefaultCivAddress_is_AC() =>
        Assert.Equal("AC", RigSettings.DefaultCivAddressFor(RigType.IcomIc905));

    [Fact]
    public void IsConfigured_ic905_with_port()
    {
        var endpoint = new RigEndpointSettings
        {
            Type = RigType.IcomIc905,
            Port = "COM905"
        };

        Assert.True(endpoint.IsConfigured);
    }

    [Fact]
    public void IsDualCapableSerialEndpoint_includes_ic905() =>
        Assert.True(RigSettings.IsDualCapableSerialEndpoint(RigType.IcomIc905));
}
