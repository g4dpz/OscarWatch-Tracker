using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using OscarWatch.Rig;

namespace OscarWatch.Tests;

public sealed class YaesuFt817DriverTests
{
    [Fact]
    public void Open_sends_cat_on()
    {
        var transport = new RecordingYaesuCatTransport();
        var driver = new YaesuFt817Driver(RigType.YaesuFt817, transport);
        driver.Open();

        Assert.Equal(YaesuFt817CatCodec.CatOn.ToArray(), transport.SentFrames[0]);
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
        Assert.Contains(transport.SentFrames, f => f.SequenceEqual(YaesuFt817CatCodec.CatOff.ToArray()));
    }

    [Fact]
    public void Pass_init_split_sets_vfo_a_and_b_frequencies()
    {
        var transport = new RecordingYaesuCatTransport();
        var controller = new RigController(_ => new YaesuFt817Driver(RigType.YaesuFt817, transport));
        var settings = new RigSettings
        {
            Enabled = true,
            Type = RigType.YaesuFt817,
            Port = "COM1",
            CatDelayMs = 0
        };

        var mode = new SatelliteTransponderMode
        {
            Type = "Voice U/V",
            DownlinkKHz = 145_960,
            UplinkKHz = 435_250,
            DownlinkMode = "FM",
            UplinkMode = "FM",
            Doppler = "NOR"
        };

        controller.Update(settings, new RigTrackingContext
        {
            TrackState = new SatelliteTrackState
            {
                Name = "AO-91",
                NoradId = "43017",
                Subpoint = new GeoCoordinate(0, 0),
                LookAngles = new LookAngles(180, 30, 800, 0)
            },
            Mode = mode,
            Corrected = DopplerFrequencyCalculator.Compute(mode, 0, 0)
        });

        Assert.Contains(transport.SentFrames, f => f.SequenceEqual(YaesuFt817CatCodec.SplitOn.ToArray()));
        Assert.Contains(transport.SentFrames, f => f.Length == 5 && f[4] == 0x01);
    }
}
