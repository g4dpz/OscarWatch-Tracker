using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
using Xunit;

namespace OscarWatch.Tests.Services;

public sealed class SnapshotBufferManagerIntegrationTests
{
    [Fact]
    public void PublishDisplaySnapshot_ReturnsValidReadOnlyList()
    {
        // Arrange
        var bufferManager = new SnapshotBufferManager();
        var states = new List<SatelliteTrackState>
        {
            CreateTestState("SAT1", "12345"),
            CreateTestState("SAT2", "23456"),
            CreateTestState("SAT3", "34567")
        };

        // Act
        var result = bufferManager.PublishDisplaySnapshot(states);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("SAT1", result[0].Name);
        Assert.Equal("SAT2", result[1].Name);
        Assert.Equal("SAT3", result[2].Name);
    }

    [Fact]
    public void PublishLiveNowSnapshot_ReturnsValidReadOnlyList()
    {
        // Arrange
        var bufferManager = new SnapshotBufferManager();
        var states = new List<SatelliteTrackState>
        {
            CreateTestState("LIVE1", "11111"),
            CreateTestState("LIVE2", "22222")
        };

        // Act
        var result = bufferManager.PublishLiveNowSnapshot(states);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("LIVE1", result[0].Name);
        Assert.Equal("LIVE2", result[1].Name);
    }

    [Fact]
    public void PublishSnapshot_HandlesEmptyList()
    {
        // Arrange
        var bufferManager = new SnapshotBufferManager();
        var emptyStates = new List<SatelliteTrackState>();

        // Act
        var displayResult = bufferManager.PublishDisplaySnapshot(emptyStates);
        var liveNowResult = bufferManager.PublishLiveNowSnapshot(emptyStates);

        // Assert
        Assert.NotNull(displayResult);
        Assert.NotNull(liveNowResult);
        Assert.Empty(displayResult);
        Assert.Empty(liveNowResult);
    }

    [Fact]
    public void PublishSnapshot_HandlesDifferentCollectionTypes()
    {
        // Arrange
        var bufferManager = new SnapshotBufferManager();
        var arrayStates = new SatelliteTrackState[]
        {
            CreateTestState("ARRAY1", "11111"),
            CreateTestState("ARRAY2", "22222")
        };

        // Act
        var result = bufferManager.PublishDisplaySnapshot(arrayStates);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("ARRAY1", result[0].Name);
        Assert.Equal("ARRAY2", result[1].Name);
    }

    [Fact]
    public void BufferGrowth_HandlesLargeCollections()
    {
        // Arrange
        var bufferManager = new SnapshotBufferManager();
        var largeCollection = new List<SatelliteTrackState>();
        
        // Create a collection larger than initial buffer size (64)
        for (int i = 0; i < 100; i++)
        {
            largeCollection.Add(CreateTestState($"SAT{i:000}", i.ToString()));
        }

        // Act
        var result = bufferManager.PublishDisplaySnapshot(largeCollection);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(100, result.Count);
        Assert.Equal("SAT000", result[0].Name);
        Assert.Equal("SAT099", result[99].Name);
        
        // Check that buffer statistics show growth
        var stats = bufferManager.GetStatistics();
        Assert.True(stats.DisplayBufferSize >= 100);
        Assert.True(stats.DisplayGrowthCount > 0);
    }

    [Fact]
    public void GetStatistics_ReturnsValidData()
    {
        // Arrange
        var bufferManager = new SnapshotBufferManager();
        var states = new List<SatelliteTrackState>
        {
            CreateTestState("STAT1", "11111"),
            CreateTestState("STAT2", "22222")
        };

        // Act
        bufferManager.PublishDisplaySnapshot(states);
        bufferManager.PublishLiveNowSnapshot(states);
        var stats = bufferManager.GetStatistics();

        // Assert
        Assert.True(stats.DisplayBufferSize > 0);
        Assert.True(stats.LiveNowBufferSize > 0);
        Assert.Equal(1, stats.DisplayPublishCount);
        Assert.Equal(1, stats.LiveNowPublishCount);
        Assert.Equal(2, stats.CurrentDisplayCount);
        Assert.Equal(2, stats.CurrentLiveNowCount);
        Assert.True(stats.DisplayUtilization >= 0.0);
        Assert.True(stats.LiveNowUtilization >= 0.0);
    }

    [Fact]
    public void CompactBuffersIfOversized_HandlesCompaction()
    {
        // Arrange
        var bufferManager = new SnapshotBufferManager();
        
        // Force buffer growth
        var largeCollection = new List<SatelliteTrackState>();
        for (int i = 0; i < 200; i++)
        {
            largeCollection.Add(CreateTestState($"LARGE{i:000}", i.ToString()));
        }
        bufferManager.PublishDisplaySnapshot(largeCollection);
        
        var statsBeforeCompact = bufferManager.GetStatistics();
        
        // Act - simulate smaller usage
        var smallCollection = new List<SatelliteTrackState>
        {
            CreateTestState("SMALL1", "11111")
        };
        bufferManager.PublishDisplaySnapshot(smallCollection);
        bufferManager.CompactBuffersIfOversized();
        
        var statsAfterCompact = bufferManager.GetStatistics();

        // Assert
        Assert.True(statsBeforeCompact.DisplayBufferSize >= 200);
        // Buffer should still work after compaction
        Assert.Equal(1, statsAfterCompact.CurrentDisplayCount);
    }

    private static SatelliteTrackState CreateTestState(string name, string noradId)
    {
        // Use non-pooled creation to avoid test contamination since these tests
        // are about SnapshotBufferManager behavior, not pooling behavior
        return new SatelliteTrackState
        {
            Name = name,
            NoradId = noradId,
            Subpoint = new GeoCoordinate(0, 0, 400_000)
        };
    }
}