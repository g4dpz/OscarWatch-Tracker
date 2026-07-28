using OscarWatch.Core.Radio;

namespace OscarWatch.Tests;

public class FlexPanBandResolverTests
{
    [Fact]
    public void TryResolveBandPans_maps_vhf_and_uhf_stream_ids()
    {
        var pans = new[]
        {
            new FlexPanState("0x40000000", 435_000_000),
            new FlexPanState("0x40000001", 145_900_000)
        };

        Assert.True(FlexPanBandResolver.TryResolveBandPans(pans, out var vhfPan, out var uhfPan));
        Assert.Equal("0x40000001", vhfPan);
        Assert.Equal("0x40000000", uhfPan);
    }

    [Fact]
    public void ResolveTargetFrequencies_uv_satellite()
    {
        FlexPanBandResolver.ResolveTargetFrequencies(
            145_960_000,
            435_148_000,
            satelliteMode: true,
            out var vhfHz,
            out var uhfHz);

        Assert.Equal(145_960_000, vhfHz);
        Assert.Equal(435_148_000, uhfHz);
    }

    [Fact]
    public void ResolveTargetFrequencies_vu_satellite()
    {
        FlexPanBandResolver.ResolveTargetFrequencies(
            435_863_000,
            145_943_000,
            satelliteMode: true,
            out var vhfHz,
            out var uhfHz);

        Assert.Equal(145_943_000, vhfHz);
        Assert.Equal(435_863_000, uhfHz);
    }
}
