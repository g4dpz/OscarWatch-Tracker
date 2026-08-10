using OscarWatch.Core.Models;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

public sealed class KenwoodThD7xDriverTests
{
    [Fact]
    public void Open_establishes_session_and_reads_band_b_frequency()
    {
        var transport = new RecordingKenwoodHtTransport
        {
            FrequencyResponse = "FO 1,0435750000,0,0,0,0"
        };
        var driver = new KenwoodThD7xDriver(RigType.KenwoodThD75, transport, catDelayMs: 0);

        driver.Open();

        Assert.Equal(
        [
            "VM 1,0\r",
            "BC 1\r",
            "FO 1\r"
        ],
        transport.SentCommands);
        Assert.Equal(435_750_000, driver.ReadFrequencyHz(RigVfo.Main));
    }

    [Fact]
    public void Open_throws_when_fo_response_is_missing()
    {
        var transport = new RecordingKenwoodHtTransport
        {
            FrequencyResponse = null
        };
        var driver = new KenwoodThD7xDriver(RigType.KenwoodThD74, transport, catDelayMs: 0);

        var ex = Assert.Throws<InvalidOperationException>(driver.Open);
        Assert.Contains("FO 1", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SetMode_usb_sends_session_mode_and_fine_step()
    {
        var transport = new RecordingKenwoodHtTransport();
        var driver = new KenwoodThD7xDriver(RigType.KenwoodThD75, transport, catDelayMs: 0);
        driver.Open();
        transport.SentCommands.Clear();
        driver.SetMode("USB");

        Assert.Equal(
        [
            "MD 1,4\r",
            "FT 1\r",
            "FS 0\r"
        ],
        transport.SentCommands);
    }

    [Fact]
    public void SetFrequency_fm_rounds_to_5khz_and_disables_fine_tune_after_mode()
    {
        var transport = new RecordingKenwoodHtTransport();
        var driver = new KenwoodThD7xDriver(RigType.KenwoodThD74, transport, catDelayMs: 0);
        driver.Open();
        driver.SetMode("FM");
        transport.SentCommands.Clear();

        Assert.True(driver.SetFrequencyHz(145_743_100));

        Assert.Contains("FQ 1,0145745000\r", transport.SentCommands);
        Assert.Contains("FT 0\r", transport.SentCommands);
        Assert.DoesNotContain(transport.SentCommands, c => c.StartsWith("FS ", StringComparison.Ordinal));
    }

    [Fact]
    public void SetFrequency_reapplies_fine_step_after_band_jump()
    {
        var transport = new RecordingKenwoodHtTransport();
        var driver = new KenwoodThD7xDriver(RigType.KenwoodThD75, transport, catDelayMs: 0);
        driver.Open();
        driver.SetMode("USB");
        Assert.True(driver.SetFrequencyHz(145_745_000));
        transport.SentCommands.Clear();

        Assert.True(driver.SetFrequencyHz(29_400_000));

        Assert.Contains("FQ 1,0029400000\r", transport.SentCommands);
        Assert.Contains("FT 1\r", transport.SentCommands);
        Assert.Contains("FS 0\r", transport.SentCommands);
    }

    [Fact]
    public void ReadFrequency_parses_fo_response()
    {
        var transport = new RecordingKenwoodHtTransport
        {
            FrequencyResponse = "FO 1,0435750000,0,0,0,0"
        };
        var driver = new KenwoodThD7xDriver(RigType.KenwoodThD75, transport, catDelayMs: 0);
        driver.Open();
        transport.SentCommands.Clear();

        Assert.Equal(435_750_000, driver.ReadFrequencyHz(RigVfo.Main));
        Assert.Equal(["FO 1\r"], transport.SentCommands);
    }
}
