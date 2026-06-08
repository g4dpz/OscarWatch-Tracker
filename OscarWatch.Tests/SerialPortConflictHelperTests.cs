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

    [Fact]
    public void HasConflict_when_dual_radios_share_same_port()
    {
        var rotator = new RotatorSettings { Enabled = true, Port = "COM3" };
        var rig = new RigSettings
        {
            Enabled = true,
            DualRadioEnabled = true,
            Downlink = new RigEndpointSettings { Type = RigType.YaesuFt817, Port = "COM3" },
            Uplink = new RigEndpointSettings { Type = RigType.YaesuFt818, Port = "COM4" }
        };
        Assert.True(SerialPortConflictHelper.HasConflict(rotator, rig));
    }

    [Fact]
    public void HasConflict_when_downlink_and_uplink_use_same_port()
    {
        var rotator = new RotatorSettings { Enabled = false, Port = "" };
        var rig = new RigSettings
        {
            Enabled = true,
            DualRadioEnabled = true,
            Downlink = new RigEndpointSettings { Type = RigType.YaesuFt817, Port = "COM5" },
            Uplink = new RigEndpointSettings { Type = RigType.YaesuFt818, Port = "COM5" }
        };
        Assert.True(SerialPortConflictHelper.TryDescribeConflict(rotator, rig, out var message));
        Assert.Contains("Downlink and uplink", message);
    }

    [Fact]
    public void HasConflict_when_gps_and_rotator_share_port()
    {
        var rotator = new RotatorSettings { Enabled = true, Port = "COM7" };
        var rig = new RigSettings { Enabled = false };
        var gps = new GpsSettings { Enabled = true, Port = "COM7" };
        Assert.True(SerialPortConflictHelper.TryDescribeConflict(rotator, rig, gps, out var message));
        Assert.Contains("GPS and rotator", message);
    }
}
