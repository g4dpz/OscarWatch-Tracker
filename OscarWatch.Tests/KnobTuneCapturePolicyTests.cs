using OscarWatch.Core.Radio;

namespace OscarWatch.Tests;

public sealed class KnobTuneCapturePolicyTests
{
    [Theory]
    [InlineData("USB")]
    [InlineData("LSB")]
    [InlineData("CW")]
    [InlineData("DATA-USB")]
    [InlineData("")]
    public void Linear_modes_use_30_hz_threshold(string mode)
    {
        Assert.Equal(KnobTuneCapturePolicy.LinearThresholdHz, KnobTuneCapturePolicy.Resolve(mode));
    }

    [Theory]
    [InlineData("FM")]
    [InlineData("FMN")]
    [InlineData("DATA-FM")]
    public void Fm_modes_use_250_hz_threshold(string mode)
    {
        Assert.Equal(KnobTuneCapturePolicy.FmThresholdHz, KnobTuneCapturePolicy.Resolve(mode));
    }
}
