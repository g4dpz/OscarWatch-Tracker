using OscarWatch.Core.Models;
using Xunit;

namespace OscarWatch.Tests;

public class ConflictDetectionSettingsTests
{
    [Fact]
    public void DefaultSettings_HaveCorrectValues()
    {
        var settings = new ConflictDetectionSettings();
        
        Assert.True(settings.ConflictDetectionEnabled);
        Assert.True(settings.QualityScoreEnabled);
        Assert.Equal(ConflictDetectionSettings.DefaultMinimumOverlapMinutes, settings.MinimumOverlapMinutes);
        Assert.Equal(ConflictDetectionSettings.DefaultElevationWeight, settings.ElevationWeight);
        Assert.Equal(ConflictDetectionSettings.DefaultDurationWeight, settings.DurationWeight);
        Assert.Equal(ConflictDetectionSettings.DefaultModeWeight, settings.ModeWeight);
        Assert.Equal(ConflictDetectionSettings.DefaultPopularityWeight, settings.PopularityWeight);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    [InlineData(15, 15)]
    [InlineData(16, 15)]
    [InlineData(-1, 1)]
    public void ClampMinimumOverlapMinutes_ClampsCorrectly(int input, int expected)
    {
        var result = ConflictDetectionSettings.ClampMinimumOverlapMinutes(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(-0.1, 0.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(0.5, 0.5)]
    [InlineData(1.0, 1.0)]
    [InlineData(1.1, 1.0)]
    public void ClampWeight_ClampsCorrectly(double input, double expected)
    {
        var result = ConflictDetectionSettings.ClampWeight(input);
        Assert.Equal(expected, result, 3);
    }

    [Fact]
    public void GetQualityWeights_ReturnsCorrectWeights()
    {
        var settings = new ConflictDetectionSettings
        {
            ElevationWeight = 0.5,
            DurationWeight = 0.3,
            ModeWeight = 0.15,
            PopularityWeight = 0.05
        };

        var weights = settings.GetQualityWeights();
        
        Assert.Equal(0.5, weights.Elevation, 3);
        Assert.Equal(0.3, weights.Duration, 3);
        Assert.Equal(0.15, weights.Mode, 3);
        Assert.Equal(0.05, weights.Popularity, 3);
    }

    [Fact]
    public void SetQualityWeights_SetsCorrectValues()
    {
        var settings = new ConflictDetectionSettings();
        var weights = new QualityWeights
        {
            Elevation = 0.6,
            Duration = 0.25,
            Mode = 0.1,
            Popularity = 0.05
        };

        settings.SetQualityWeights(weights);
        
        Assert.Equal(0.6, settings.ElevationWeight, 3);
        Assert.Equal(0.25, settings.DurationWeight, 3);
        Assert.Equal(0.1, settings.ModeWeight, 3);
        Assert.Equal(0.05, settings.PopularityWeight, 3);
    }

    [Fact]
    public void NormalizeWeights_NormalizesCorrectly()
    {
        var settings = new ConflictDetectionSettings
        {
            ElevationWeight = 0.8,  // Total will be 2.0
            DurationWeight = 0.6,
            ModeWeight = 0.4,
            PopularityWeight = 0.2
        };

        settings.NormalizeWeights();
        
        // Should be normalized to sum to 1.0
        Assert.Equal(0.4, settings.ElevationWeight, 3);
        Assert.Equal(0.3, settings.DurationWeight, 3);
        Assert.Equal(0.2, settings.ModeWeight, 3);
        Assert.Equal(0.1, settings.PopularityWeight, 3);
        
        var sum = settings.ElevationWeight + settings.DurationWeight + settings.ModeWeight + settings.PopularityWeight;
        Assert.Equal(1.0, sum, 3);
    }

    [Fact]
    public void NormalizeWeights_ResetsToDefaultsWhenAllZero()
    {
        var settings = new ConflictDetectionSettings
        {
            ElevationWeight = 0.0,
            DurationWeight = 0.0,
            ModeWeight = 0.0,
            PopularityWeight = 0.0
        };

        settings.NormalizeWeights();
        
        Assert.Equal(ConflictDetectionSettings.DefaultElevationWeight, settings.ElevationWeight);
        Assert.Equal(ConflictDetectionSettings.DefaultDurationWeight, settings.DurationWeight);
        Assert.Equal(ConflictDetectionSettings.DefaultModeWeight, settings.ModeWeight);
        Assert.Equal(ConflictDetectionSettings.DefaultPopularityWeight, settings.PopularityWeight);
    }

    [Fact]
    public void GetMinimumOverlapThreshold_ReturnsCorrectTimeSpan()
    {
        var settings = new ConflictDetectionSettings
        {
            MinimumOverlapMinutes = 5
        };

        var threshold = settings.GetMinimumOverlapThreshold();
        
        Assert.Equal(TimeSpan.FromMinutes(5), threshold);
    }

    [Fact]
    public void NormalizeWeights_ClampsOutOfRangeValues()
    {
        var settings = new ConflictDetectionSettings
        {
            ElevationWeight = -0.1,  // Below minimum
            DurationWeight = 1.2,   // Above maximum
            ModeWeight = 0.3,
            PopularityWeight = 0.4
        };

        settings.NormalizeWeights();
        
        // Values should be clamped and then normalized
        Assert.True(settings.ElevationWeight >= 0.0);
        Assert.True(settings.DurationWeight <= 1.0);
        
        var sum = settings.ElevationWeight + settings.DurationWeight + settings.ModeWeight + settings.PopularityWeight;
        Assert.Equal(1.0, sum, 2);
    }
}