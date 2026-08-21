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
    public void No_conflict_when_uplink_is_dummy()
    {
        var rotator = new RotatorSettings { Enabled = true, Port = "COM3" };
        var rig = new RigSettings
        {
            Enabled = true,
            DualRadioEnabled = true,
            Downlink = new RigEndpointSettings { Type = RigType.YaesuFt817, Port = "COM4" },
            Uplink = new RigEndpointSettings { Type = RigType.Dummy, Port = "COM3" }
        };
        Assert.False(SerialPortConflictHelper.HasConflict(rotator, rig));
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

    [Fact]
    public void No_gps_conflict_when_gps_uses_gpsd_network()
    {
        var rotator = new RotatorSettings { Enabled = true, Port = "COM7" };
        var rig = new RigSettings { Enabled = true, Type = RigType.IcomIc910, Port = "COM8" };
        var gps = new GpsSettings
        {
            Enabled = true,
            ConnectionKind = GpsConnectionKind.Gpsd,
            GpsdHost = "127.0.0.1",
            Port = "COM7"
        };
        Assert.False(SerialPortConflictHelper.TryDescribeConflict(rotator, rig, gps, out _));
    }

    [Fact]
    public void No_conflict_when_rotator_uses_tcp_serial()
    {
        var rotator = new RotatorSettings
        {
            Enabled = true,
            Type = RotatorType.YaesuGs232,
            TransportKind = RotatorTransportKind.Tcp,
            Port = "COM3",
            NetworkHost = "127.0.0.1",
            NetworkPort = 4001
        };
        var rig = new RigSettings { Enabled = true, Type = RigType.IcomIc910, Port = "COM3" };
        Assert.False(SerialPortConflictHelper.HasConflict(rotator, rig));
    }

    [Fact]
    public void No_conflict_when_rotator_is_urc_tcp()
    {
        var rotator = new RotatorSettings
        {
            Enabled = true,
            Type = RotatorType.UrcTcp,
            Port = "COM3",
            NetworkHost = "127.0.0.1",
            NetworkPort = 1111
        };
        var rig = new RigSettings { Enabled = true, Type = RigType.IcomIc910, Port = "COM3" };
        Assert.False(SerialPortConflictHelper.HasConflict(rotator, rig));
    }

    [Fact]
    public void HasConflict_when_rt21_azimuth_and_elevation_share_port()
    {
        var rotator = new RotatorSettings
        {
            Enabled = true,
            Type = RotatorType.GreenHeronRt21,
            Port = "COM5",
            ElevationPort = "COM5"
        };
        var rig = new RigSettings { Enabled = false };
        Assert.True(SerialPortConflictHelper.TryDescribeConflict(rotator, rig, out var message));
        Assert.Contains("azimuth and elevation", message);
    }

    [Fact]
    public void HasConflict_when_rt21_elevation_shares_radio_port()
    {
        var rotator = new RotatorSettings
        {
            Enabled = true,
            Type = RotatorType.GreenHeronRt21,
            Port = "COM3",
            ElevationPort = "COM4"
        };
        var rig = new RigSettings { Enabled = true, Type = RigType.IcomIc910, Port = "COM4" };
        Assert.True(SerialPortConflictHelper.TryDescribeConflict(rotator, rig, out var message));
        Assert.Contains("Rotator and radio both use COM4", message);
    }

    [Fact]
    public void HasConflict_when_gps_and_rt21_elevation_share_port()
    {
        var rotator = new RotatorSettings
        {
            Enabled = true,
            Type = RotatorType.GreenHeronRt21,
            Port = "COM3",
            ElevationPort = "COM7"
        };
        var rig = new RigSettings { Enabled = false };
        var gps = new GpsSettings { Enabled = true, Port = "COM7" };
        Assert.True(SerialPortConflictHelper.TryDescribeConflict(rotator, rig, gps, out var message));
        Assert.Contains("GPS and rotator", message);
    }

    [Fact]
    public void No_conflict_when_rt21_ports_are_distinct_from_radio()
    {
        var rotator = new RotatorSettings
        {
            Enabled = true,
            Type = RotatorType.GreenHeronRt21,
            Port = "COM3",
            ElevationPort = "COM4"
        };
        var rig = new RigSettings { Enabled = true, Type = RigType.IcomIc910, Port = "COM5" };
        Assert.False(SerialPortConflictHelper.HasConflict(rotator, rig));
    }
}
