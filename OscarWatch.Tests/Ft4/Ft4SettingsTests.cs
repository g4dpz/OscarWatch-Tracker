using OscarWatch.Core.Ft4;

namespace OscarWatch.Tests.Ft4;

public sealed class Ft4SettingsTests
{
    [Fact]
    public void Uplink_calibration_round_trips_and_clamps()
    {
        var settings = new Ft4Settings();
        settings.SetUplinkCalibrationKHz("RS-44", 0.35);
        Assert.Equal(0.35, settings.GetUplinkCalibrationKHz("rs-44"));

        settings.SetUplinkCalibrationKHz("RS-44", 50);
        Assert.Equal(20.0, settings.GetUplinkCalibrationKHz("RS-44"));

        settings.SetUplinkCalibrationKHz("RS-44", 0);
        Assert.Equal(0, settings.GetUplinkCalibrationKHz("RS-44"));
        Assert.Empty(settings.UplinkCalibrationKHzBySatellite);
    }

    [Fact]
    public void Defaults_match_operator_expectations()
    {
        var settings = new Ft4Settings();
        Assert.True(settings.SkipRrr);
        Assert.Equal(Ft4PttMethod.Vox, settings.PttMethod);
        Assert.Equal(Ft4PttLine.Rts, settings.PttLine);
        Assert.Equal(200, settings.PttLeadMs);
        Assert.Equal(100, settings.PttTailMs);
        Assert.True(settings.HoldTxFrequency);
        Assert.True(settings.AudioDopplerTx);
        Assert.True(settings.AudioDopplerRx);
        Assert.Equal(12, settings.DecodeFontSize);
    }
}
