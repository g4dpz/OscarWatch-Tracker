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
    public void SetSatelliteMode_on_sends_SA_then_verifies()
    {
        var transport = new RecordingKenwoodCatTransport();
        var driver = new KenwoodTs2000Driver(transport);
        driver.Open();
        driver.SetSatelliteMode(true);

        Assert.Contains("SA10100000;", transport.SentCommands);
        Assert.Contains("SA;", transport.SentCommands);
        Assert.True(transport.SentCommands.IndexOf("SA10100000;") < transport.SentCommands.IndexOf("SA;"));
    }

    [Fact]
    public void SetSatelliteMode_on_not_confirmed_keeps_non_satellite_state()
    {
        var transport = new RecordingKenwoodCatTransport { SatelliteStatusOn = false };
        var driver = new KenwoodTs2000Driver(transport);
        driver.Open();
        driver.SetSatelliteMode(true);
        transport.SentCommands.Clear();

        driver.SelectVfo(RigVfo.Sub);
        driver.SetFrequencyHz(145_900_000);

        Assert.Contains("FR1;", transport.SentCommands);
        Assert.DoesNotContain(transport.SentCommands, c => c == "SA;");
    }

    [Fact]
    public void SetSatelliteMode_off_sends_SA0()
    {
        var transport = new RecordingKenwoodCatTransport();
        var driver = new KenwoodTs2000Driver(transport);
        driver.Open();
        driver.SetSatelliteMode(true);
        transport.SentCommands.Clear();
        driver.SetSatelliteMode(false);

        Assert.Contains("SA0;", transport.SentCommands);
        Assert.DoesNotContain(transport.SentCommands, c => c == "SA;");
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
    public void SupportsVfoExchange_is_true()
    {
        var driver = new KenwoodTs2000Driver(new RecordingKenwoodCatTransport());
        Assert.True(driver.SupportsVfoExchange);
    }

    [Fact]
    public void ExchangeVfos_in_satellite_mode_swaps_FA_and_FB()
    {
        var transport = new RecordingKenwoodCatTransport { FaHz = 145_900_000, FbHz = 435_700_000 };
        var driver = new KenwoodTs2000Driver(transport);
        driver.Open();
        driver.SetSatelliteMode(true);
        transport.SentCommands.Clear();
        driver.ExchangeVfos();

        Assert.Contains("FA00435700000;", transport.SentCommands);
        Assert.Contains("FB00145900000;", transport.SentCommands);
        Assert.Equal(435_700_000, transport.FaHz);
        Assert.Equal(145_900_000, transport.FbHz);
    }

    [Fact]
    public void ExchangeVfos_noop_when_not_in_satellite_mode()
    {
        var transport = new RecordingKenwoodCatTransport();
        var driver = new KenwoodTs2000Driver(transport);
        driver.Open();
        driver.ExchangeVfos();

        Assert.DoesNotContain(transport.SentCommands, c => c.StartsWith("FA", StringComparison.Ordinal));
    }

    [Fact]
    public void SetToneHz_encode_sends_TN_command()
    {
        var transport = new RecordingKenwoodCatTransport();
        var driver = new KenwoodTs2000Driver(transport);
        driver.Open();
        driver.SetToneHz(67.0, squelchTone: false);

        Assert.Contains("TN01;", transport.SentCommands);
        Assert.DoesNotContain(transport.SentCommands, c => c.StartsWith("CN", StringComparison.Ordinal));
    }

    [Fact]
    public void SetToneHz_squelch_sends_CN_command()
    {
        var transport = new RecordingKenwoodCatTransport();
        var driver = new KenwoodTs2000Driver(transport);
        driver.Open();
        driver.SetToneHz(67.0, squelchTone: true);

        Assert.Contains("CN01;", transport.SentCommands);
        Assert.DoesNotContain(transport.SentCommands, c => c.StartsWith("TN", StringComparison.Ordinal));
    }

    [Fact]
    public void SetToneOn_sends_TO_command()
    {
        var transport = new RecordingKenwoodCatTransport();
        var driver = new KenwoodTs2000Driver(transport);
        driver.Open();
        driver.SetToneOn(true);

        Assert.Contains("TO1;", transport.SentCommands);
        Assert.DoesNotContain(transport.SentCommands, c => c.StartsWith("CT", StringComparison.Ordinal));
    }

    [Fact]
    public void SetToneSquelchOn_sends_CT_command()
    {
        var transport = new RecordingKenwoodCatTransport();
        var driver = new KenwoodTs2000Driver(transport);
        driver.Open();
        driver.SetToneSquelchOn(true);

        Assert.Contains("CT1;", transport.SentCommands);
        Assert.DoesNotContain(transport.SentCommands, c => c.StartsWith("TO", StringComparison.Ordinal));
    }

    [Fact]
    public void SetMode_on_sub_in_satellite_mode_selects_sub_control()
    {
        var transport = new RecordingKenwoodCatTransport();
        var driver = new KenwoodTs2000Driver(transport);
        driver.Open();
        driver.SetSatelliteMode(true);
        driver.SelectVfo(RigVfo.Sub);
        driver.SetMode("FM");

        var mdIndex = transport.SentCommands.IndexOf("MD4;");
        Assert.True(mdIndex >= 0);
        Assert.Equal("DC01;", transport.SentCommands[mdIndex - 1]);
        Assert.Equal("DC00;", transport.SentCommands[mdIndex + 1]);
    }

    [Fact]
    public void SetToneHz_on_sub_in_satellite_mode_selects_sub_control()
    {
        var transport = new RecordingKenwoodCatTransport();
        var driver = new KenwoodTs2000Driver(transport);
        driver.Open();
        driver.SetSatelliteMode(true);
        driver.SelectVfo(RigVfo.Sub);
        driver.SetToneHz(67.0, squelchTone: false);

        var tnIndex = transport.SentCommands.IndexOf("TN01;");
        Assert.True(tnIndex >= 0);
        Assert.Equal("DC01;", transport.SentCommands[tnIndex - 1]);
        Assert.Equal("DC00;", transport.SentCommands[tnIndex + 1]);
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
