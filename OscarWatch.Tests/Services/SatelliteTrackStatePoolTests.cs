using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
using Xunit;

namespace OscarWatch.Tests.Services;

public sealed class SatelliteTrackStatePoolTests
{
    [Fact]
    public void Rent_ReturnsValidObject()
    {
        // Act
        var state = SatelliteTrackStatePool.Rent();
        
        // Assert
        Assert.NotNull(state);
        Assert.NotNull(state.Name);
        Assert.NotNull(state.NoradId);
        Assert.NotNull(state.Subpoint);
        
        // Clean up
        SatelliteTrackStatePool.Return(state);
    }
    
    [Fact]
    public void Return_ResetsObjectProperties()
    {
        // Arrange
        var state = SatelliteTrackStatePool.Rent();
        state.Name = "Test Satellite";
        state.NoradId = "12345";
        state.MotionHeadingDeg = 42.0;
        
        // Act
        SatelliteTrackStatePool.Return(state);
        var newState = SatelliteTrackStatePool.Rent();
        
        // Assert
        Assert.Equal(string.Empty, newState.Name);
        // Note: NoradId and other properties will be set during CreatePooled usage
        
        // Clean up
        SatelliteTrackStatePool.Return(newState);
    }
    
    [Fact]
    public void CreatePooled_InitializesAllProperties()
    {
        // Arrange
        var name = "Test Satellite";
        var noradId = "12345";
        var subpoint = new GeoCoordinate(45.0, -123.0, 400_000);
        var lookAngles = new LookAngles(180.0, 45.0, 1000.0, 2.5);
        var motionHeading = 135.0;
        var isSunlit = false;
        
        // Act
        var state = SatelliteTrackState.CreatePooled(
            name: name,
            noradId: noradId,
            subpoint: subpoint,
            lookAngles: lookAngles,
            motionHeadingDeg: motionHeading,
            isSunlit: isSunlit);
        
        // Assert
        Assert.Equal(name, state.Name);
        Assert.Equal(noradId, state.NoradId);
        Assert.Equal(subpoint, state.Subpoint);
        Assert.Equal(lookAngles, state.LookAngles);
        Assert.Equal(motionHeading, state.MotionHeadingDeg);
        Assert.Equal(isSunlit, state.IsSunlit);
        
        // Clean up
        SatelliteTrackStatePool.Return(state);
    }
    
    [Fact]
    public void ReturnRange_HandlesMultipleObjects()
    {
        // Arrange
        var states = new List<SatelliteTrackState>();
        for (int i = 0; i < 5; i++)
        {
            states.Add(SatelliteTrackStatePool.Rent());
        }
        
        // Act
        SatelliteTrackStatePool.ReturnRange(states);
        
        // Assert - should be able to rent again without issues
        var newState = SatelliteTrackStatePool.Rent();
        Assert.NotNull(newState);
        
        // Clean up
        SatelliteTrackStatePool.Return(newState);
    }
    
    [Fact]
    public void GetStatistics_ReturnsValidData()
    {
        // Arrange - clear any previous state
        var initialStats = SatelliteTrackStatePool.GetStatistics();
        
        // Act
        var state1 = SatelliteTrackStatePool.Rent();
        var state2 = SatelliteTrackStatePool.Rent();
        var midStats = SatelliteTrackStatePool.GetStatistics();
        
        SatelliteTrackStatePool.Return(state1);
        var finalStats = SatelliteTrackStatePool.GetStatistics();
        
        // Assert
        Assert.True(finalStats.RentCount >= 2);
        Assert.True(finalStats.ReturnCount >= 1);
        Assert.True(finalStats.CreateCount > 0);
        
        // Clean up
        SatelliteTrackStatePool.Return(state2);
    }
    
    [Fact]
    public void Pool_HandlesHighVolume()
    {
        // Arrange
        const int iterations = 1000;
        var states = new List<SatelliteTrackState>(iterations);
        
        // Get baseline statistics before our test
        var baselineStats = SatelliteTrackStatePool.GetStatistics();
        
        // Act - rent many objects
        for (int i = 0; i < iterations; i++)
        {
            var state = SatelliteTrackState.CreatePooled(
                name: $"Sat{i}",
                noradId: i.ToString(),
                subpoint: new GeoCoordinate(i % 180 - 90, i % 360 - 180, 400_000));
            states.Add(state);
        }
        
        // Return all objects
        SatelliteTrackStatePool.ReturnRange(states);
        
        // Get final statistics
        var finalStats = SatelliteTrackStatePool.GetStatistics();
        
        // Assert - use deltas to avoid cross-test interference
        var rentDelta = finalStats.RentCount - baselineStats.RentCount;
        var returnDelta = finalStats.ReturnCount - baselineStats.ReturnCount;
        
        Assert.True(rentDelta >= iterations, $"Expected >= {iterations} rent operations, got {rentDelta}");
        Assert.True(returnDelta >= iterations, $"Expected >= {iterations} return operations, got {returnDelta}");
        Assert.True(finalStats.HitRatio >= 0.0 && finalStats.HitRatio <= 1.0);
        Assert.True(finalStats.UtilizationPercentage >= 0.0);
    }
}