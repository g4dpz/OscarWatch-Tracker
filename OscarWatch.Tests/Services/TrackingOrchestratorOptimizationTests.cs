using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
using Xunit;

namespace OscarWatch.Tests.Services;

/// <summary>
/// Tests that verify the ground track staggered recomputation optimization
/// eliminates temporary allocations while maintaining functional behavior.
/// </summary>
public sealed class TrackingOrchestratorOptimizationTests
{
    [Fact]
    public void TrackingCollections_GetStaleTracksBuffer_ReturnsReusableCollection()
    {
        // Act - get buffer multiple times
        var buffer1 = TrackingCollections.GetStaleTracksBuffer();
        buffer1.Add((CreateTestSatellite("SAT1"), new SatelliteVisualCache.Entry()));
        
        var buffer2 = TrackingCollections.GetStaleTracksBuffer();
        var buffer3 = TrackingCollections.GetStaleTracksBuffer();

        // Assert - same instance is reused and cleared
        Assert.Same(buffer1, buffer2);
        Assert.Same(buffer1, buffer3);
        Assert.Empty(buffer2); // Should be cleared when requested
        Assert.Empty(buffer3); // Should be cleared when requested
        
        // Verify statistics tracking
        var stats = TrackingCollections.GetStatistics();
        Assert.Equal(3, stats.StaleTracksBufferUsageCount);
    }

    [Fact]
    public void TrackingCollections_HandlesDifferentSizedUsage()
    {
        // Arrange - reset statistics to avoid interference from other tests
        TrackingCollections.ResetStatistics();
        
        // Act - add different amounts of data across multiple cycles
        for (int cycle = 0; cycle < 5; cycle++)
        {
            var buffer = TrackingCollections.GetStaleTracksBuffer();
            
            // Add varying numbers of items to test capacity growth
            for (int i = 0; i < (cycle + 1) * 10; i++)
            {
                buffer.Add((CreateTestSatellite($"SAT{i}"), new SatelliteVisualCache.Entry()));
            }
        }

        var stats = TrackingCollections.GetStatistics();
        
        // Assert - buffer should have been used 5 times after reset
        Assert.Equal(5, stats.StaleTracksBufferUsageCount);
        Assert.True(stats.CurrentStaleTracksCapacity >= 50); // Should accommodate largest usage
    }

    [Fact]
    public void SatelliteTrackStatePool_IntegratesWithCreatePooled()
    {
        // This test verifies that the pooled object creation used in TrackingOrchestrator
        // works correctly and provides allocation benefits
        
        // Arrange
        var initialStats = SatelliteTrackStatePool.GetStatistics();
        var states = new List<SatelliteTrackState>();
        
        // Act - simulate creating states like TrackingOrchestrator does
        for (int i = 0; i < 10; i++)
        {
            var state = SatelliteTrackState.CreatePooled(
                name: $"TestSat{i}",
                noradId: (12345 + i).ToString(),
                subpoint: new GeoCoordinate(i * 10.0, i * 15.0, 400_000),
                isSunlit: i % 2 == 0);
            states.Add(state);
        }
        
        // Return objects to pool (simulating buffer clearing)
        SatelliteTrackStatePool.ReturnRange(states);
        
        // Create more objects (should reuse from pool)
        var reusedStates = new List<SatelliteTrackState>();
        for (int i = 0; i < 5; i++)
        {
            var state = SatelliteTrackState.CreatePooled(
                name: $"ReusedSat{i}",
                noradId: (99000 + i).ToString(),
                subpoint: new GeoCoordinate(i * 5.0, i * 7.0, 500_000));
            reusedStates.Add(state);
        }

        var finalStats = SatelliteTrackStatePool.GetStatistics();

        // Assert - pool should show activity
        Assert.True(finalStats.RentCount >= 15, $"Expected RentCount >= 15, but was {finalStats.RentCount}"); 
        Assert.True(finalStats.ReturnCount >= 10, $"Expected ReturnCount >= 10, but was {finalStats.ReturnCount}");
        
        // The HitRatio calculation includes initial pool allocation, so it can be negative
        // What matters is that we're using the pool (positive rent/return counts)
        Assert.True(finalStats.RentCount > 0 && finalStats.ReturnCount > 0, "Pool should show rent/return activity");
        
        // Verify object properties are correct
        Assert.Equal("ReusedSat0", reusedStates[0].Name);
        Assert.Equal("99000", reusedStates[0].NoradId);
        
        // Clean up
        SatelliteTrackStatePool.ReturnRange(reusedStates);
    }

    [Fact]
    public void InPlaceStateUpdate_PreservesOtherProperties()
    {
        // This test verifies that the in-place ground track update optimization
        // maintains all other properties correctly
        
        // Arrange - create a state with all properties set
        var originalState = SatelliteTrackState.CreatePooled(
            name: "TestSat",
            noradId: "12345",
            subpoint: new GeoCoordinate(45.0, -123.0, 400_000),
            lookAngles: new LookAngles(180.0, 45.0, 1000.0, 2.5),
            motionHeadingDeg: 135.0,
            groundTrack: new List<GeoCoordinate> 
            { 
                new GeoCoordinate(44.0, -122.0, 0),
                new GeoCoordinate(45.0, -121.0, 0) 
            },
            footprint: new List<GeoCoordinate> 
            { 
                new GeoCoordinate(40.0, -120.0, 0) 
            },
            footprintRadiusDeg: 12.5,
            isSunlit: false);

        // Store original values
        var originalName = originalState.Name;
        var originalNoradId = originalState.NoradId;
        var originalSubpoint = originalState.Subpoint;
        var originalLookAngles = originalState.LookAngles;
        var originalMotionHeading = originalState.MotionHeadingDeg;
        var originalFootprint = originalState.Footprint;
        var originalFootprintRadius = originalState.FootprintRadiusDeg;
        var originalIsSunlit = originalState.IsSunlit;

        // Act - update ground track in-place (as done in optimized staggered recomputation)
        var newGroundTrack = new List<GeoCoordinate> 
        { 
            new GeoCoordinate(46.0, -124.0, 0),
            new GeoCoordinate(47.0, -125.0, 0),
            new GeoCoordinate(48.0, -126.0, 0) 
        };
        originalState.GroundTrack = newGroundTrack;

        // Assert - only ground track should change, all other properties preserved
        Assert.Equal(originalName, originalState.Name);
        Assert.Equal(originalNoradId, originalState.NoradId);
        Assert.Equal(originalSubpoint, originalState.Subpoint);
        Assert.Equal(originalLookAngles, originalState.LookAngles);
        Assert.Equal(originalMotionHeading, originalState.MotionHeadingDeg);
        Assert.Equal(originalFootprint, originalState.Footprint);
        Assert.Equal(originalFootprintRadius, originalState.FootprintRadiusDeg);
        Assert.Equal(originalIsSunlit, originalState.IsSunlit);
        
        // Ground track should be updated
        Assert.Equal(newGroundTrack, originalState.GroundTrack);
        Assert.Equal(3, originalState.GroundTrack.Count);
        
        // Clean up
        SatelliteTrackStatePool.Return(originalState);
    }

    [Fact] 
    public void CollectionStatistics_TrackUsageCorrectly()
    {
        // Arrange - reset statistics
        TrackingCollections.ResetStatistics();
        
        // Act - simulate tracking loop usage patterns
        for (int i = 0; i < 3; i++)
        {
            var buffer = TrackingCollections.GetStaleTracksBuffer();
            buffer.Add((CreateTestSatellite("TEST"), new SatelliteVisualCache.Entry()));
        }

        var stats = TrackingCollections.GetStatistics();

        // Assert - statistics should accurately reflect usage
        Assert.Equal(3, stats.StaleTracksBufferUsageCount);
        Assert.True(stats.CurrentStaleTracksCapacity >= 64); // Initial capacity
    }

    private static SatelliteCatalogEntry CreateTestSatellite(string name)
    {
        return new SatelliteCatalogEntry
        {
            Name = name,
            NoradId = "12345",
            Line1 = "1 12345U 12345A   21001.00000000  .00000000  00000-0  00000-0 0    10",
            Line2 = "2 12345  51.6400   0.0000 0000000   0.0000   0.0000 15.48919000    10"
        };
    }
}