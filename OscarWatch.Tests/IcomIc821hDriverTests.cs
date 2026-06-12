using OscarWatch.Core.Models;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

public sealed class IcomIc821hDriverTests
{
    [Fact]
    public void RigType_is_IcomIc821h()
    {
        var driver = new IcomIc821hDriver("COM1", 19200, "4C");
        Assert.Equal(RigType.IcomIc821h, driver.RigType);
    }

    [Fact]
    public void Factory_creates_IcomIc821h_driver()
    {
        var driver = RigDriverFactory.Create(new RigSettings { Type = RigType.IcomIc821h });
        Assert.IsType<IcomIc821hDriver>(driver);
    }

    [Fact]
    public void SupportsVfoExchange_is_false()
    {
        var driver = new IcomIc821hDriver("COM1", 19200, "4C");
        Assert.False(driver.SupportsVfoExchange);
    }

    [Fact]
    public void Satellite_mode_inverts_main_sub_band_access_bytes()
    {
        var transport = new RecordingIcomCivTransport();
        var driver = new IcomIc821hDriver(transport);
        transport.Open();
        driver.Open();

        driver.SetSatelliteMode(true);
        driver.SelectVfo(RigVfo.Main, force: true);
        Assert.Contains("07d1", transport.SentCommandBodies);

        driver.SelectVfo(RigVfo.Sub, force: true);
        Assert.Contains("07d0", transport.SentCommandBodies);
    }

    [Fact]
    public void Non_satellite_mode_uses_normal_main_sub_bytes()
    {
        var transport = new RecordingIcomCivTransport();
        var driver = new IcomIc821hDriver(transport);
        transport.Open();
        driver.Open();

        driver.SetSatelliteMode(false);
        driver.SelectVfo(RigVfo.Main, force: true);
        Assert.Contains("07d0", transport.SentCommandBodies);

        driver.SelectVfo(RigVfo.Sub, force: true);
        Assert.Contains("07d1", transport.SentCommandBodies);
    }

    [Fact]
    public void SetSatelliteMode_sends_ic910_era_bytes()
    {
        var transport = new RecordingIcomCivTransport();
        var driver = new IcomIc821hDriver(transport);
        transport.Open();
        driver.Open();

        driver.SetSatelliteMode(true);
        Assert.Contains("1a0701", transport.SentCommandBodies);

        driver.SetSatelliteMode(false);
        Assert.Contains("1a0700", transport.SentCommandBodies);
    }

    [Fact]
    public void SetSplitOn_does_not_send_split_command()
    {
        var transport = new RecordingIcomCivTransport();
        var driver = new IcomIc821hDriver(transport);
        transport.Open();
        driver.Open();

        var before = transport.CommandCount;
        driver.SetSplitOn(true);
        driver.SetSplitOn(false);
        Assert.Equal(before, transport.CommandCount);
    }

    [Fact]
    public void EstablishSatelliteVfoState_selects_main_rx_band()
    {
        var transport = new RecordingIcomCivTransport();
        var driver = new IcomIc821hDriver(transport);
        transport.Open();
        driver.Open();

        driver.SetSatelliteMode(true);
        transport.SentCommandBodies.Clear();
        driver.EstablishSatelliteVfoState();
        Assert.Equal("07d1", transport.SentCommandBodies[^1]);
    }
}
