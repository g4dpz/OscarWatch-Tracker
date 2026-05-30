using OscarWatch.Core.Models;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

public sealed class IcomCivDriverTests
{
    private static readonly byte[] CivNak = [0xFE, 0xFE, 0x60, 0x00, 0xFA, 0xFD];

    [Fact]
    public void SetFrequencyHz_ack_updates_cached_read_when_live_read_fails()
    {
        var transport = new RecordingIcomCivTransport { MainHz = 435_750_000 };
        var driver = new IcomIc910Driver(transport);
        driver.Open();
        driver.SelectVfo(RigVfo.Main);

        Assert.Equal(435_750_000, driver.ReadFrequencyHz(RigVfo.Main));
        Assert.True(driver.SetFrequencyHz(435_751_000));

        transport.NextReadResponse = [];
        Assert.Equal(435_751_000, driver.ReadFrequencyHz(RigVfo.Main));
    }

    [Fact]
    public void SetFrequencyHz_without_ack_does_not_update_cached_read()
    {
        var transport = new RecordingIcomCivTransport { MainHz = 435_750_000 };
        var driver = new IcomIc910Driver(transport);
        driver.Open();
        driver.SelectVfo(RigVfo.Main);

        Assert.Equal(435_750_000, driver.ReadFrequencyHz(RigVfo.Main));

        transport.SetFrequencyResponses.Enqueue(CivNak);
        transport.SetFrequencyResponses.Enqueue(CivNak);
        Assert.False(driver.SetFrequencyHz(435_760_000));

        transport.NextReadResponse = [];
        Assert.Equal(435_750_000, driver.ReadFrequencyHz(RigVfo.Main));
        Assert.Equal(2, transport.SetFrequencyCommandCount);
    }

    [Fact]
    public void SetFrequencyHz_empty_response_does_not_update_cached_read()
    {
        var transport = new RecordingIcomCivTransport { MainHz = 435_750_000 };
        var driver = new IcomIc910Driver(transport);
        driver.Open();
        driver.SelectVfo(RigVfo.Main);

        Assert.Equal(435_750_000, driver.ReadFrequencyHz(RigVfo.Main));

        transport.SetFrequencyResponses.Enqueue([]);
        transport.SetFrequencyResponses.Enqueue([]);
        Assert.False(driver.SetFrequencyHz(435_760_000));

        transport.NextReadResponse = [];
        Assert.Equal(435_750_000, driver.ReadFrequencyHz(RigVfo.Main));
        Assert.Equal(2, transport.SetFrequencyCommandCount);
    }

    [Fact]
    public void SetFrequencyHz_retries_once_then_succeeds()
    {
        var transport = new RecordingIcomCivTransport { MainHz = 435_750_000 };
        var driver = new IcomIc910Driver(transport);
        driver.Open();
        driver.SelectVfo(RigVfo.Main);

        transport.SetFrequencyResponses.Enqueue(CivNak);
        Assert.True(driver.SetFrequencyHz(435_760_000));
        Assert.Equal(2, transport.SetFrequencyCommandCount);

        transport.NextReadResponse = [];
        Assert.Equal(435_760_000, driver.ReadFrequencyHz(RigVfo.Main));
    }

    [Fact]
    public void SelectVfo_retries_once_after_nak()
    {
        var transport = new RecordingIcomCivTransport();
        var driver = new IcomIc910Driver(transport);
        driver.Open();

        transport.CommandResponses.Enqueue(CivNak);
        driver.SelectVfo(RigVfo.Sub, force: true);

        Assert.Equal(2, transport.CommandCount);
    }

    [Fact]
    public void SelectVfo_without_ack_assumes_success_and_skips_redundant_select()
    {
        var transport = new RecordingIcomCivTransport();
        var driver = new IcomIc910Driver(transport);
        driver.Open();

        transport.CommandResponses.Enqueue([]);
        driver.SelectVfo(RigVfo.Main, force: true);
        driver.SelectVfo(RigVfo.Main);

        Assert.Equal(2, transport.CommandCount);
    }

    [Fact]
    public void SelectVfo_without_ack_retries_once_when_changing_vfo()
    {
        var transport = new RecordingIcomCivTransport();
        var driver = new IcomIc910Driver(transport);
        driver.Open();

        transport.CommandResponses.Enqueue([]);
        transport.CommandResponses.Enqueue([]);
        driver.SelectVfo(RigVfo.Main, force: true);

        Assert.Equal(2, transport.CommandCount);
    }
}
