using OscarWatch.Core.Cloudlog;
using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;

namespace OscarWatch.Tests;

public class TransponderOperatingModesTests
{
    [Fact]
    public void SupportsCwUplinkToggle_true_for_linear_voice_not_fm_or_beacon()
    {
        var mode = new SatelliteTransponderMode
        {
            Type = "Voice U/V",
            DownlinkKHz = 145_960,
            UplinkKHz = 435_148,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        Assert.True(TransponderOperatingModes.SupportsCwUplinkToggle(mode));
    }

    [Fact]
    public void SupportsCwUplinkToggle_false_when_uplink_already_cw_in_database()
    {
        var mode = new SatelliteTransponderMode
        {
            Type = "CW Transponder",
            DownlinkKHz = 145_865,
            UplinkKHz = 435_110.1,
            DownlinkMode = "USB",
            UplinkMode = "CW",
            Doppler = "REV"
        };

        Assert.False(TransponderOperatingModes.SupportsCwUplinkToggle(mode));
    }

    [Fact]
    public void GetEffectiveModes_overrides_uplink_and_downlink()
    {
        var mode = new SatelliteTransponderMode
        {
            Type = "SSB Transponder",
            DownlinkKHz = 435_850,
            UplinkKHz = 145_950,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        var (tx, rx) = TransponderOperatingModes.GetEffectiveModes(mode, cwUplink: true);

        Assert.Equal("CW", tx);
        Assert.Equal("CW", rx);
    }

    [Fact]
    public void CloudlogTryCreate_uses_cw_on_both_vfos_when_override_enabled()
    {
        var mode = new SatelliteTransponderMode
        {
            Type = "Voice U/V",
            DownlinkKHz = 145_960,
            UplinkKHz = 435_148,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        var corrected = DopplerFrequencyCalculator.Compute(mode, 0, 0);
        var update = CloudlogRadioMapper.TryCreate("AO-73", mode, corrected, cwUplink: true);

        Assert.NotNull(update);
        Assert.Equal("CW", update.UplinkMode);
        Assert.Equal("CW", update.DownlinkMode);
    }
}
