using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

public sealed class YaesuFt991DriverTests
{
    [Fact]
    public void Open_unlocks_dial()
    {
        var transport = new RecordingYaesuNewCatTransport();
        var driver = new YaesuFt991Driver(RigType.YaesuFt991, transport);
        driver.Open();

        Assert.Contains("LK0;", transport.SentCommands);
    }

    [Fact]
    public void SetFrequencyHz_on_main_uses_fa_command()
    {
        var transport = new RecordingYaesuNewCatTransport();
        var driver = new YaesuFt991Driver(RigType.YaesuFt991, transport);
        driver.Open();
        transport.SentCommands.Clear();

        driver.SelectVfo(RigVfo.Main);
        Assert.True(driver.SetFrequencyHz(145_960_000));

        Assert.Contains("FA145960000;", transport.SentCommands);
    }

    [Fact]
    public void SetMode_fm_locks_dial()
    {
        var transport = new RecordingYaesuNewCatTransport();
        var driver = new YaesuFt991Driver(RigType.YaesuFt991, transport);
        driver.Open();
        transport.SentCommands.Clear();

        driver.SetMode("FM");

        Assert.Contains("MD04;", transport.SentCommands);
        Assert.Contains("LK1;", transport.SentCommands);
    }

    [Fact]
    public void SetToneHz_encode_sends_cn_and_ct()
    {
        var transport = new RecordingYaesuNewCatTransport();
        var driver = new YaesuFt991Driver(RigType.YaesuFt991, transport);
        driver.Open();
        transport.SentCommands.Clear();

        driver.SetToneHz(67.0, squelchTone: false);

        Assert.Contains("CN00000;CT02;", transport.SentCommands);
    }

    [Fact]
    public void SetSplitOn_true_sends_ft3()
    {
        var transport = new RecordingYaesuNewCatTransport();
        var driver = new YaesuFt991Driver(RigType.YaesuFt991, transport);
        driver.Open();
        transport.SentCommands.Clear();

        driver.SetSplitOn(true);

        Assert.Contains("FT3;", transport.SentCommands);
    }

    [Fact]
    public void SetSplitOn_false_sends_ft2()
    {
        var transport = new RecordingYaesuNewCatTransport();
        var driver = new YaesuFt991Driver(RigType.YaesuFt991, transport);
        driver.Open();
        transport.SentCommands.Clear();

        driver.SetSplitOn(false);

        Assert.Contains("FT2;", transport.SentCommands);
    }

    [Fact]
    public void SetFrequencyHz_on_main_with_split_copies_vfo_a_to_b()
    {
        var transport = new RecordingYaesuNewCatTransport();
        var driver = new YaesuFt991Driver(RigType.YaesuFt991, transport);
        driver.Open();
        transport.SentCommands.Clear();

        driver.SetSplitOn(true);
        transport.SentCommands.Clear();
        driver.SelectVfo(RigVfo.Main);
        Assert.True(driver.SetFrequencyHz(145_960_000));

        Assert.Equal("FA145960000;", transport.SentCommands[0]);
        Assert.Equal("AB;", transport.SentCommands[1]);
    }

    [Fact]
    public void SetFrequencyHz_on_vfo_b_does_not_copy()
    {
        var transport = new RecordingYaesuNewCatTransport();
        var driver = new YaesuFt991Driver(RigType.YaesuFt991, transport);
        driver.Open();
        transport.SentCommands.Clear();

        driver.SetSplitOn(true);
        transport.SentCommands.Clear();
        driver.SelectVfo(RigVfo.VfoB);
        Assert.True(driver.SetFrequencyHz(435_250_000));

        Assert.Single(transport.SentCommands);
        Assert.Equal("FB435250000;", transport.SentCommands[0]);
    }

    [Fact]
    public void SetFrequencyHz_returns_false_when_set_rejected()
    {
        var transport = new RecordingYaesuNewCatTransport { FailSets = true };
        var driver = new YaesuFt991Driver(RigType.YaesuFt991, transport);
        driver.Open();
        transport.SentCommands.Clear();

        driver.SelectVfo(RigVfo.Main);
        Assert.False(driver.SetFrequencyHz(145_960_000));
        Assert.Contains("FA145960000;", transport.SentCommands);
    }

    [Fact]
    public void SendCommand_does_not_require_echo_reply()
    {
        // Real Yaesu sets return no reply; the recording transport mirrors that.
        var transport = new RecordingYaesuNewCatTransport();
        transport.Open();

        Assert.True(transport.SendCommand("FA145960000;"));
        Assert.Null(transport.Transact("MD0;")); // no canned read reply
        Assert.Equal(2, transport.SentCommands.Count);
    }
}
