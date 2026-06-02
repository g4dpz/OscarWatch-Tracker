using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

public sealed class YaesuFt817DriverTests
{
    [Fact]
    public void Open_sends_dial_lock_off()
    {
        var transport = new RecordingYaesuCatTransport();
        var driver = new YaesuFt817Driver(RigType.YaesuFt817, transport);
        driver.Open();

        Assert.Equal(YaesuFt817CatCodec.DialLockOff.ToArray(), transport.SentFrames[0]);
    }

    [Fact]
    public void SetMode_USB_unlocks_dial_SetMode_FM_locks_dial()
    {
        var transport = new RecordingYaesuCatTransport();
        var driver = new YaesuFt817Driver(RigType.YaesuFt817, transport);
        driver.Open();
        transport.SentFrames.Clear();

        driver.SetMode("USB");
        Assert.Contains(transport.SentFrames, f => f.SequenceEqual(YaesuFt817CatCodec.DialLockOff.ToArray()));

        transport.SentFrames.Clear();
        driver.SetMode("FM");
        Assert.Contains(transport.SentFrames, f => f.SequenceEqual(YaesuFt817CatCodec.DialLockOn.ToArray()));
    }

    [Fact]
    public void SetSplitOn_sends_split_opcode()
    {
        var transport = new RecordingYaesuCatTransport();
        var driver = new YaesuFt817Driver(RigType.YaesuFt817, transport);
        driver.Open();
        driver.SetSplitOn(true);

        Assert.Contains(transport.SentFrames, f => f.SequenceEqual(YaesuFt817CatCodec.SplitOn.ToArray()));
    }

    [Fact]
    public void SelectVfoB_toggles_before_set_frequency()
    {
        var transport = new RecordingYaesuCatTransport();
        var driver = new YaesuFt817Driver(RigType.YaesuFt817, transport);
        driver.Open();
        driver.SetSplitOn(true);
        driver.SelectVfo(RigVfo.VfoB);
        driver.SetFrequencyHz(435_825_000);

        Assert.Contains(transport.SentFrames, f => f.SequenceEqual(YaesuFt817CatCodec.ToggleVfo.ToArray()));
        Assert.Contains(transport.SentFrames, f => f.Length == 5 && f[4] == 0x01);
    }

    [Fact]
    public void Ft818_reports_correct_rig_type()
    {
        var driver = new YaesuFt818Driver(new RecordingYaesuCatTransport());
        Assert.Equal(RigType.YaesuFt818, driver.RigType);
    }

    [Fact]
    public void SupportsVfoExchange_is_false()
    {
        var driver = new YaesuFt817Driver(RigType.YaesuFt817, new RecordingYaesuCatTransport());
        Assert.False(driver.SupportsVfoExchange);
    }

    [Fact]
    public void Dispose_sends_cat_off()
    {
        var transport = new RecordingYaesuCatTransport();
        var driver = new YaesuFt817Driver(RigType.YaesuFt817, transport);
        driver.Open();
        driver.Dispose();
        Assert.Contains(transport.SentFrames, f => f.SequenceEqual(YaesuFt817CatCodec.DialLockOff.ToArray()));
    }

}
