using OscarWatch.Core.Ft4;

namespace OscarWatch.Tests.Ft4;

public sealed class Ft4SpectrumAnalyzerTests
{
    [Fact]
    public void Pixel_to_hz_and_back_round_trips_at_edges()
    {
        Assert.Equal(200, Ft4SpectrumAnalyzer.PixelToHz(0, 400, 200, 3000), 3);
        Assert.Equal(3000, Ft4SpectrumAnalyzer.PixelToHz(400, 400, 200, 3000), 3);
        Assert.Equal(0, Ft4SpectrumAnalyzer.HzToPixel(200, 400, 200, 3000), 3);
        Assert.Equal(400, Ft4SpectrumAnalyzer.HzToPixel(3000, 400, 200, 3000), 3);
    }

    [Fact]
    public void Tone_at_1500_hz_peaks_near_mid_passband()
    {
        const int sampleRate = 12000;
        const int n = 2048;
        var samples = new float[n];
        for (var i = 0; i < n; i++)
            samples[i] = (float)Math.Sin(2 * Math.PI * 1500 * i / sampleRate);

        var bins = new float[400];
        Assert.True(Ft4SpectrumAnalyzer.TryComputePassband(samples, sampleRate, bins));

        var peak = 0;
        for (var i = 1; i < bins.Length; i++)
        {
            if (bins[i] > bins[peak])
                peak = i;
        }

        var peakHz = Ft4SpectrumAnalyzer.PixelToHz(peak, bins.Length - 1, 200, 3000);
        Assert.InRange(peakHz, 1400, 1600);
    }

    [Fact]
    public void Noise_floor_estimate_is_below_tone_peak()
    {
        const int sampleRate = 12000;
        const int n = 2048;
        var samples = new float[n];
        for (var i = 0; i < n; i++)
            samples[i] = 0.05f * (float)Math.Sin(2 * Math.PI * 1500 * i / sampleRate);

        var bins = new float[400];
        Assert.True(Ft4SpectrumAnalyzer.TryComputePassband(samples, sampleRate, bins));
        var floor = Ft4SpectrumAnalyzer.EstimateNoiseFloorDb(bins);
        var peak = bins.Max();
        Assert.True(peak > floor + 6);
    }
}
