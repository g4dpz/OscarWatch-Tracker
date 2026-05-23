using OscarWatch.Core.Models;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

public sealed class IcomIc9100DriverTests
{
    [Fact]
    public void RigType_is_IcomIc9100()
    {
        var driver = new IcomIc9100Driver("COM1", 19200, "7C");
        Assert.Equal(RigType.IcomIc9100, driver.RigType);
    }

    [Fact]
    public void SupportsTracking_is_true()
    {
        var driver = new IcomIc9100Driver("COM1", 19200, "7C");
        Assert.True(driver.SupportsTracking);
    }

    [Fact]
    public void Factory_creates_IcomIc9100_driver()
    {
        var driver = RigDriverFactory.Create(new RigSettings { Type = RigType.IcomIc9100 });
        Assert.IsType<IcomIc9100Driver>(driver);
    }
}
