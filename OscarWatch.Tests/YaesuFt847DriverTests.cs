using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

public sealed class YaesuFt847DriverTests
{
    [Fact]
    public void Open_sends_cat_on()
    {
        var transport = new RecordingYaesuCatTransport();
        var driver = new YaesuFt847Driver(transport);
        driver.Open();

        Assert.True(transport.SentFrames.Count >= 1);
        Assert.Equal(YaesuFt847CatCodec.CatOn.ToArray(), transport.SentFrames[0]);
    }

    [Fact]
    public void SetSatelliteMode_and_frequencies_use_sat_vfos()
    {
        var transport = new RecordingYaesuCatTransport();
        var driver = new YaesuFt847Driver(transport);
        driver.Open();
        driver.SetSatelliteMode(true);
        driver.SelectVfo(RigVfo.Main);
        driver.SetFrequencyHz(435_750_000);
        driver.SelectVfo(RigVfo.Sub);
        driver.SetFrequencyHz(145_900_010);

        Assert.Contains(transport.SentFrames, f => f.SequenceEqual(YaesuFt847CatCodec.SatelliteModeOn.ToArray()));
        Assert.Contains(transport.SentFrames, f => f.Length == 5 && f[4] == 0x11);
        Assert.Contains(transport.SentFrames, f => f.Length == 5 && f[4] == 0x21);
    }

    [Fact]
    public void SetMode_FM_sends_wide_fm_byte()
    {
        var transport = new RecordingYaesuCatTransport();
        var driver = new YaesuFt847Driver(transport);
        driver.Open();
        driver.SetSatelliteMode(true);
        driver.SetMode("FM");

        Assert.Contains(transport.SentFrames, f => f.Length == 5 && f[0] == 0x08);
    }

    [Fact]
    public void SupportsVfoExchange_is_false()
    {
        var driver = new YaesuFt847Driver(new RecordingYaesuCatTransport());
        Assert.False(driver.SupportsVfoExchange);
    }

    [Fact]
    public void Dispose_sends_cat_off()
    {
        var transport = new RecordingYaesuCatTransport();
        var driver = new YaesuFt847Driver(transport);
        driver.Open();
        driver.Dispose();
        Assert.Contains(transport.SentFrames, f => f.SequenceEqual(YaesuFt847CatCodec.CatOff.ToArray()));
    }
}
