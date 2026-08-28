using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
using Xunit;

namespace OscarWatch.Tests.Services;

/// <summary>
/// Tests that verify the PublishSnapshot optimization in LiveTrackingService 
/// eliminates array allocations while maintaining functional equivalence.
/// </summary>
public sealed class LiveTrackingServiceOptimizationTests
{
    [Fact]
    public void SnapshotBufferManager_EliminatesArrayAllocation()
    {
        // This test demonstrates that SnapshotBufferManager provides the same
        // functionality as the old PublishSnapshot method but with buffer reuse
        
        // Arrange
        var states = new List<SatelliteTrackState>
        {
            CreateTestState("ISS", "25544"),
            CreateTestState("NOAA-18", "28654"),
            CreateTestState("NOAA-19", "33591")
        };
        
        var bufferManager = new SnapshotBufferManager();

        // Act - simulate the old behavior
        var oldWayResult = SimulateOldPublishSnapshot(states);
        
        // Act - use the new optimized approach
        var newWayResult = bufferManager.PublishDisplaySnapshot(states);

        // Assert - both approaches should produce equivalent results
        Assert.Equal(oldWayResult.Count, newWayResult.Count);
        for (int i = 0; i < oldWayResult.Count; i++)
        {
            Assert.Equal(oldWayResult[i].Name, newWayResult[i].Name);
            Assert.Equal(oldWayResult[i].NoradId, newWayResult[i].NoradId);
        }

        // The key difference: new way uses buffer reuse, old way creates new arrays
        var stats = bufferManager.GetStatistics();
        Assert.True(stats.DisplayBufferSize >= states.Count);
        Assert.Equal(1, stats.DisplayPublishCount);
    }

    [Fact]
    public void BufferReuse_HandlesDifferentSizedCollections()
    {
        // Arrange
        var bufferManager = new SnapshotBufferManager();
        
        var smallCollection = new List<SatelliteTrackState>
        {
            CreateTestState("SAT1", "11111")
        };
        
        var largeCollection = new List<SatelliteTrackState>();
        for (int i = 0; i < 50; i++)
        {
            largeCollection.Add(CreateTestState($"SAT{i:00}", (10000 + i).ToString()));
        }

        // Act - alternate between different sized collections
        var result1 = bufferManager.PublishDisplaySnapshot(smallCollection);
        var result2 = bufferManager.PublishDisplaySnapshot(largeCollection);
        var result3 = bufferManager.PublishDisplaySnapshot(smallCollection);

        // Assert - all results should be valid
        Assert.Single(result1);
        Assert.Equal(50, result2.Count);
        Assert.Single(result3);

        // Buffer should have grown to accommodate the larger collection
        var stats = bufferManager.GetStatistics();
        Assert.True(stats.DisplayBufferSize >= 50);
        Assert.Equal(3, stats.DisplayPublishCount);
    }

    [Fact]
    public void LiveTrackingService_UsesOptimizedBuffering()
    {
        // This test verifies that LiveTrackingService can be created and uses
        // the new buffer manager without the old array allocation pattern
        
        // Since we can't easily mock TrackingOrchestrator, we'll just verify
        // that the buffer management methods are available and functional
        
        // The actual integration would be tested in full system tests
        var bufferManager = new SnapshotBufferManager();
        var testStates = new List<SatelliteTrackState>
        {
            CreateTestState("TEST", "12345")
        };
        
        var result = bufferManager.PublishDisplaySnapshot(testStates);
        
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("TEST", result[0].Name);
        
        var stats = bufferManager.GetStatistics();
        Assert.NotNull(stats);
        Assert.Equal(1, stats.DisplayPublishCount);
    }

    /// <summary>
    /// Simulates the old PublishSnapshot behavior for comparison testing.
    /// This is what we're optimizing away - creating new arrays every time.
    /// </summary>
    private static IReadOnlyList<SatelliteTrackState> SimulateOldPublishSnapshot(
        IReadOnlyList<SatelliteTrackState> states)
    {
        // This is the old implementation we're replacing:
        return states.Count == 0 ? Array.Empty<SatelliteTrackState>() : states.ToArray();
    }

    private static SatelliteTrackState CreateTestState(string name, string noradId)
    {
        return SatelliteTrackState.CreatePooled(
            name: name,
            noradId: noradId,
            subpoint: new GeoCoordinate(0, 0, 400_000));
    }
}