using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

public sealed class KenwoodTs2000DriverTests
{
    [Fact]
    public void Open_sends_autoinfo_off()
    {
        var transport = new RecordingKenwoodCatTransport();
        var driver = new KenwoodTs2000Driver(transport);
        driver.Open();

        Assert.Contains("AI0;", transport.SentCommands);
    }

    [Fact]
    public void SetSatelliteMode_queries_SA()
    {
        var transport = new RecordingKenwoodCatTransport();
        var driver = new KenwoodTs2000Driver(transport);
        driver.Open();
        driver.SetSatelliteMode(true);

        Assert.Contains("SA;", transport.SentCommands);
    }

    [Fact]
    public void SetFrequency_in_satellite_mode_uses_FA_and_FB()
    {
        var transport = new RecordingKenwoodCatTransport();
        var driver = new KenwoodTs2000Driver(transport);
        driver.Open();
        driver.SetSatelliteMode(true);
        driver.SelectVfo(RigVfo.Main);
        driver.SetFrequencyHz(435_750_000);
        driver.SelectVfo(RigVfo.Sub);
        driver.SetFrequencyHz(145_900_000);

        Assert.Contains("FA00435750000;", transport.SentCommands);
        Assert.Contains("FB00145900000;", transport.SentCommands);
        Assert.DoesNotContain(transport.SentCommands, c => c.StartsWith("FR", StringComparison.Ordinal));
    }

    [Fact]
    public void SupportsVfoExchange_is_false()
    {
        var driver = new KenwoodTs2000Driver(new RecordingKenwoodCatTransport());
        Assert.False(driver.SupportsVfoExchange);
    }

    [Fact]
    public void SetToneHz_sends_CN_command()
    {
        var transport = new RecordingKenwoodCatTransport();
        var driver = new KenwoodTs2000Driver(transport);
        driver.Open();
        driver.SetToneHz(67.0, squelchTone: false);

        Assert.Contains("CN01;", transport.SentCommands);
    }

    [Fact]
    public void ReadFrequencyHz_parses_FA_reply()
    {
        var transport = new RecordingKenwoodCatTransport();
        var driver = new KenwoodTs2000Driver(transport);
        driver.Open();

        var hz = driver.ReadFrequencyHz(RigVfo.Main);
        Assert.Equal(435_750_000, hz);
    }
}
