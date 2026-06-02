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
