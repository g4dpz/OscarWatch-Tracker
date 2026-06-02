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
