using OscarWatch.Core.Models;
using OscarWatch.Core.Rotator;

namespace OscarWatch.Tests;

public sealed class SmartAzimuthPassPreviewTests
{
    [Fact]
    public void TryApply_returns_false_when_smart450_disabled()
    {
        var samples = CreateSamples(10, 20, 30);
        Assert.False(SmartAzimuthPassPreview.TryApply(samples, smartAzimuth450: false, maxAzimuthDeg: 450));
        Assert.All(samples, s => Assert.Null(s.CommandAzimuthDeg));
    }

    [Fact]
    public void TryApply_returns_false_when_max_az_is_360()
    {
        var samples = CreateSamples(10, 20, 30);
        Assert.False(SmartAzimuthPassPreview.TryApply(samples, smartAzimuth450: true, maxAzimuthDeg: 360));
        Assert.All(samples, s => Assert.Null(s.CommandAzimuthDeg));
    }

    [Fact]
    public void TryApply_seeds_from_aos_with_compass_primary()
    {
        var samples = CreateSamples(80, 70, 60);
        Assert.True(SmartAzimuthPassPreview.TryApply(samples, smartAzimuth450: true, maxAzimuthDeg: 450));
        Assert.Equal(80, samples[0].CommandAzimuthDeg);
        Assert.Equal(70, samples[1].CommandAzimuthDeg);
        Assert.Equal(60, samples[2].CommandAzimuthDeg);
        Assert.False(SmartAzimuthPassPreview.UsesExtendedBand(samples));
    }

    [Fact]
    public void TryApply_east_descent_commits_to_extended_when_samples_cross_north()
    {
        var samples = CreateSamples(80, 50, 25, 15, 355);
        Assert.True(SmartAzimuthPassPreview.TryApply(samples, smartAzimuth450: true, maxAzimuthDeg: 450));

        Assert.Equal(80, samples[0].CommandAzimuthDeg);
        Assert.Equal(50, samples[1].CommandAzimuthDeg);
        Assert.Equal(385, samples[2].CommandAzimuthDeg);
        Assert.Equal(375, samples[3].CommandAzimuthDeg);
        Assert.True(SmartAzimuthPassPreview.UsesExtendedBand(samples));
    }

    [Fact]
    public void TryApply_northbound_without_north_crossing_stays_primary()
    {
        // RS-44-class: descending toward north, min az still east of 0°.
        var samples = CreateSamples(145, 96, 78, 46, 40, 28, 20);
        Assert.True(SmartAzimuthPassPreview.TryApply(samples, smartAzimuth450: true, maxAzimuthDeg: 450));

        Assert.Equal(46, samples[3].CommandAzimuthDeg);
        Assert.Equal(40, samples[4].CommandAzimuthDeg);
        Assert.Equal(28, samples[5].CommandAzimuthDeg);
        Assert.False(SmartAzimuthPassPreview.UsesExtendedBand(samples));
    }

    [Fact]
    public void TryApply_matches_resolve_command_az_sequence_with_lookahead()
    {
        var compass = new[] { 50.0, 15.0, 355.0 };
        var samples = CreateSamples(compass);
        Assert.True(SmartAzimuthPassPreview.TryApply(samples, smartAzimuth450: true, maxAzimuthDeg: 450));

        double? last = null;
        for (var i = 0; i < compass.Length; i++)
        {
            double? next = i + 1 < compass.Length ? compass[i + 1] : null;
            var expected = RotatorAzimuthPlanner.ResolveCommandAz(last, compass[i], 450, next);
            Assert.Equal(expected, samples[i].CommandAzimuthDeg);
            last = expected;
        }
    }

    [Fact]
    public void TryApply_north_wrap_sequence_uses_extended()
    {
        var samples = CreateSamples(350, 10, 20);
        Assert.True(SmartAzimuthPassPreview.TryApply(samples, smartAzimuth450: true, maxAzimuthDeg: 450));
        Assert.Equal(350, samples[0].CommandAzimuthDeg);
        Assert.Equal(370, samples[1].CommandAzimuthDeg);
        Assert.Equal(380, samples[2].CommandAzimuthDeg);
        Assert.True(SmartAzimuthPassPreview.UsesExtendedBand(samples));
    }

    private static PassPolarPlotSample[] CreateSamples(params double[] azimuthDeg)
    {
        var start = new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);
        var samples = new PassPolarPlotSample[azimuthDeg.Length];
        for (var i = 0; i < azimuthDeg.Length; i++)
        {
            samples[i] = new PassPolarPlotSample
            {
                Utc = start.AddSeconds(i * 15),
                AzimuthDeg = azimuthDeg[i],
                ElevationDeg = 20
            };
        }

        return samples;
    }
}
