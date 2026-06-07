using OscarWatch.Core.Hardware;
using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

public sealed class DualRadioConfigHelperTests
{
    [Fact]
    public void IsIncomplete_false_when_rig_disabled()
    {
        var rig = DualRig(downPort: "COM3");
        rig.Enabled = false;
        Assert.False(DualRadioConfigHelper.IsIncomplete(rig));
    }

    [Fact]
    public void IsIncomplete_false_when_dual_disabled()
    {
        var rig = new RigSettings
        {
            Enabled = true,
            DualRadioEnabled = false,
            Type = RigType.IcomIc910,
            Port = "COM3"
        };
        Assert.False(DualRadioConfigHelper.IsIncomplete(rig));
    }

    [Fact]
    public void IsIncomplete_true_when_only_downlink_configured()
    {
        var rig = DualRig(downPort: "COM3");
        Assert.True(DualRadioConfigHelper.IsIncomplete(rig));
        Assert.Equal(DualRadioConfigHelper.MissingUplinkCode, DualRadioConfigHelper.IncompleteCode(rig));
    }

    [Fact]
    public void IsIncomplete_true_when_only_uplink_configured()
    {
        var rig = DualRig(upPort: "COM4");
        Assert.True(DualRadioConfigHelper.IsIncomplete(rig));
        Assert.Equal(DualRadioConfigHelper.MissingDownlinkCode, DualRadioConfigHelper.IncompleteCode(rig));
    }

    [Fact]
    public void IsIncomplete_true_when_neither_leg_configured()
    {
        var rig = DualRig();
        Assert.True(DualRadioConfigHelper.IsIncomplete(rig));
        Assert.Equal(DualRadioConfigHelper.MissingBothCode, DualRadioConfigHelper.IncompleteCode(rig));
    }

    [Fact]
    public void IsIncomplete_false_when_both_legs_configured()
    {
        var rig = DualRig(downPort: "COM3", upPort: "COM4");
        Assert.False(DualRadioConfigHelper.IsIncomplete(rig));
        Assert.Equal("", DualRadioConfigHelper.IncompleteCode(rig));
    }

    private static RigSettings DualRig(string? downPort = null, string? upPort = null) => new()
    {
        Enabled = true,
        DualRadioEnabled = true,
        Downlink = new RigEndpointSettings
        {
            Type = RigType.YaesuFt817,
            Port = downPort ?? ""
        },
        Uplink = new RigEndpointSettings
        {
            Type = RigType.YaesuFt818,
            Port = upPort ?? ""
        }
    };
}
