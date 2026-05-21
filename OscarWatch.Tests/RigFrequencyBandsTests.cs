using OscarWatch.Core.Radio;

namespace OscarWatch.Tests;

public class RigFrequencyBandsTests
{
    [Theory]
    [InlineData(435_667_000, 435_670_000, true)]
    [InlineData(435_667_000, 145_937_000, false)]
    [InlineData(145_850_000, 145_860_000, true)]
    public void IsPlausibleReceiveRead_detects_uv_mismatch(long referenceHz, long readHz, bool expected) =>
        Assert.Equal(expected, RigFrequencyBands.IsPlausibleReceiveRead(referenceHz, readHz));
}
