using OscarWatch.Core.Models;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

public sealed class RigDriverFactoryTests
{
    [Fact]
    public void Create_endpoint_ft817_returns_driver_not_exception()
    {
        var driver = RigDriverFactory.Create(new RigEndpointSettings
        {
            Type = RigType.YaesuFt817,
            Port = "COM_TEST",
            BaudRate = 4800
        });

        Assert.Equal(RigType.YaesuFt817, driver.RigType);
    }

    [Fact]
    public void Create_endpoint_ic705_returns_driver_with_civ_address()
    {
        var driver = RigDriverFactory.Create(new RigEndpointSettings
        {
            Type = RigType.IcomIc705,
            Port = "COM705",
            BaudRate = 115200,
            CivAddress = "A4"
        });

        Assert.Equal(RigType.IcomIc705, driver.RigType);
    }

    [Fact]
    public void Create_settings_ic705_when_not_dual_throws()
    {
        Assert.Throws<InvalidOperationException>(() => RigDriverFactory.Create(new RigSettings
        {
            Type = RigType.IcomIc705,
            Port = "COM705"
        }));
    }

    [Theory]
    [InlineData(RigType.IcomIc706)]
    [InlineData(RigType.IcomIc706Mkii)]
    [InlineData(RigType.IcomIc706MkiiG)]
    public void Create_endpoint_ic706_series_returns_driver_with_civ_address(RigType rigType)
    {
        var driver = RigDriverFactory.Create(new RigEndpointSettings
        {
            Type = rigType,
            Port = "COM706",
            BaudRate = 19200,
            CivAddress = RigSettings.DefaultCivAddressFor(rigType)
        });

        Assert.Equal(rigType, driver.RigType);
    }

    [Theory]
    [InlineData(RigType.IcomIc706)]
    [InlineData(RigType.IcomIc706Mkii)]
    [InlineData(RigType.IcomIc706MkiiG)]
    public void Create_settings_ic706_series_when_not_dual_throws(RigType rigType)
    {
        Assert.Throws<InvalidOperationException>(() => RigDriverFactory.Create(new RigSettings
        {
            Type = rigType,
            Port = "COM706"
        }));
    }

    [Fact]
    public void Create_endpoint_ft991_returns_driver()
    {
        var driver = RigDriverFactory.Create(new RigEndpointSettings
        {
            Type = RigType.YaesuFt991,
            Port = "COM991",
            BaudRate = 38400
        });

        Assert.Equal(RigType.YaesuFt991, driver.RigType);
    }

    [Fact]
    public void Create_settings_ft991_when_not_dual_throws()
    {
        Assert.Throws<InvalidOperationException>(() => RigDriverFactory.Create(new RigSettings
        {
            Type = RigType.YaesuFt991,
            Port = "COM991"
        }));
    }

    [Fact]
    public void Create_endpoint_ftx1_returns_driver()
    {
        var driver = RigDriverFactory.Create(new RigEndpointSettings
        {
            Type = RigType.YaesuFtx1,
            Port = "COMFTX1",
            BaudRate = 38400
        });

        Assert.Equal(RigType.YaesuFtx1, driver.RigType);
    }

    [Fact]
    public void Create_settings_ftx1_when_not_dual_throws()
    {
        Assert.Throws<InvalidOperationException>(() => RigDriverFactory.Create(new RigSettings
        {
            Type = RigType.YaesuFtx1,
            Port = "COMFTX1"
        }));
    }

    [Fact]
    public void Create_settings_ft817_when_dual_enabled_throws()
    {
        Assert.Throws<InvalidOperationException>(() => RigDriverFactory.Create(new RigSettings
        {
            DualRadioEnabled = true,
            Type = RigType.YaesuFt817,
            Port = "COM1"
        }));
    }
}
