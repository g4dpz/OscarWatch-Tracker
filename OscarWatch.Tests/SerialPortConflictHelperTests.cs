using OscarWatch.Core.Hardware;
using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

public class SerialPortConflictHelperTests
{
    [Fact]
    public void HasConflict_when_both_use_same_port()
    {
        var rotator = new RotatorSettings { Enabled = true, Port = "COM3" };
        var rig = new RigSettings { Enabled = true, Type = RigType.IcomIc910, Port = "COM3" };
        Assert.True(SerialPortConflictHelper.HasConflict(rotator, rig));
    }

    [Fact]
    public void No_conflict_for_dummy_rig()
    {
        var rotator = new RotatorSettings { Enabled = true, Port = "COM3" };
        var rig = new RigSettings { Enabled = true, Type = RigType.Dummy, Port = "COM3" };
        Assert.False(SerialPortConflictHelper.HasConflict(rotator, rig));
    }

    [Fact]
    public void No_conflict_when_ports_differ()
    {
        var rotator = new RotatorSettings { Enabled = true, Port = "COM3" };
        var rig = new RigSettings { Enabled = true, Type = RigType.IcomIc910, Port = "COM4" };
        Assert.False(SerialPortConflictHelper.HasConflict(rotator, rig));
    }
}
