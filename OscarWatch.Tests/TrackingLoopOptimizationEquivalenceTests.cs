using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
using Xunit;

namespace OscarWatch.Tests;

/// <summary>
/// Tests that ensure the tracking loop memory optimizations maintain 
/// functional equivalence with the original implementation.
/// </summary>
public sealed class TrackingLoopOptimizationEquivalenceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void SatelliteTrackStateCreation_ProducesEquivalentObjects(int satelliteCount)
    {
        // Generate test data
        var testData = GenerateTestSatelliteData(satelliteCount);
        
        // Create objects using old approach
        var oldObjects = new List<SatelliteTrackState>();
        foreach (var data in testData)
        {
            oldObjects.Add(new SatelliteTrackState
            {
                Name = data.Name,
                NoradId = data.NoradId,
                Subpoint = data.Subpoint,
                LookAngles = data.LookAngles,
                MotionHeadingDeg = data.MotionHeading,
                GroundTrack = data.GroundTrack,
                NextOrbitGroundTrack = data.NextOrbitGroundTrack,
                Footprint = data.Footprint,
                FootprintRadiusDeg = data.FootprintRadius,
                IsSunlit = data.IsSunlit
            });
        }

        // Create objects using new pooled approach
        var newObjects = new List<SatelliteTrackState>();
        foreach (var data in testData)
        {
            newObjects.Add(SatelliteTrackState.CreatePooled(
                name: data.Name,
                noradId: data.NoradId,
                subpoint: data.Subpoint,
                lookAngles: data.LookAngles,
                motionHeadingDeg: data.MotionHeading,
                groundTrack: data.GroundTrack,
                nextOrbitGroundTrack: data.NextOrbitGroundTrack,
                footprint: data.Footprint,
                footprintRadiusDeg: data.FootprintRadius,
                isSunlit: data.IsSunlit));
        }

        // Assert equivalence
        Assert.Equal(oldObjects.Count, newObjects.Count);
        for (int i = 0; i < oldObjects.Count; i++)
        {
            AssertSatelliteStatesEquivalent(oldObjects[i], newObjects[i]);
        }

        // Clean up pooled objects
        SatelliteTrackStatePool.ReturnRange(newObjects);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(50)]
    [InlineData(200)]
    public void SnapshotBuffering_ProducesEquivalentResults(int stateCount)
    {
        // Create test states
        var states = new List<SatelliteTrackState>();
        for (int i = 0; i < stateCount; i++)
        {
            states.Add(SatelliteTrackState.CreatePooled(
                name: $"TestSat{i}",
                noradId: i.ToString(),
                subpoint: new GeoCoordinate(i * 10.0, i * 15.0, 400_000),
                isSunlit: i % 2 == 0));
        }

        // Test old approach
        var oldSnapshot = SimulateOldPublishSnapshot(states);

        // Test new approach
        var bufferManager = new SnapshotBufferManager();
        var newSnapshot = bufferManager.PublishDisplaySnapshot(states);

        // Assert equivalence
        Assert.Equal(oldSnapshot.Count, newSnapshot.Count);
        for (int i = 0; i < oldSnapshot.Count; i++)
        {
            AssertSatelliteStatesEquivalent(oldSnapshot[i], newSnapshot[i]);
        }

        // Clean up
        SatelliteTrackStatePool.ReturnRange(states);
    }

    [Fact]
    public void InPlaceGroundTrackUpdate_PreservesAllOtherProperties()
    {
        // Create a state with all properties set
        var originalState = SatelliteTrackState.CreatePooled(
            name: "TestSat",
            noradId: "12345",
            subpoint: new GeoCoordinate(45.0, -123.0, 400_000),
            lookAngles: new LookAngles(180.0, 45.0, 1000.0, 2.5),
            motionHeadingDeg: 135.0,
            groundTrack: new List<GeoCoordinate> { new GeoCoordinate(44.0, -122.0, 0) },
            nextOrbitGroundTrack: new List<GeoCoordinate> { new GeoCoordinate(46.0, -124.0, 0) },
            footprint: new List<GeoCoordinate> { new GeoCoordinate(40.0, -120.0, 0) },
            footprintRadiusDeg: 12.5,
            isSunlit: false);

        // Store original values
        var originalProps = CaptureStateProperties(originalState);

        // Simulate the old approach (create new object)
        var oldUpdatedState = new SatelliteTrackState
        {
            Name = originalState.Name,
            NoradId = originalState.NoradId,
            Subpoint = originalState.Subpoint,
            LookAngles = originalState.LookAngles,
            MotionHeadingDeg = originalState.MotionHeadingDeg,
            GroundTrack = new List<GeoCoordinate> { new GeoCoordinate(47.0, -125.0, 0) }, // Updated
            NextOrbitGroundTrack = originalState.NextOrbitGroundTrack,
            Footprint = originalState.Footprint,
            FootprintRadiusDeg = originalState.FootprintRadiusDeg,
            IsSunlit = originalState.IsSunlit
        };

        // Simulate the new approach (in-place update)
        var newGroundTrack = new List<GeoCoordinate> { new GeoCoordinate(47.0, -125.0, 0) };
        originalState.GroundTrack = newGroundTrack;

        // Assert equivalence except for the updated ground track
        Assert.Equal(oldUpdatedState.Name, originalState.Name);
        Assert.Equal(oldUpdatedState.NoradId, originalState.NoradId);
        Assert.Equal(oldUpdatedState.Subpoint, originalState.Subpoint);
        Assert.Equal(oldUpdatedState.LookAngles, originalState.LookAngles);
        Assert.Equal(oldUpdatedState.MotionHeadingDeg, originalState.MotionHeadingDeg);
        Assert.Equal(oldUpdatedState.NextOrbitGroundTrack, originalState.NextOrbitGroundTrack);
        Assert.Equal(oldUpdatedState.Footprint, originalState.Footprint);
        Assert.Equal(oldUpdatedState.FootprintRadiusDeg, originalState.FootprintRadiusDeg);
        Assert.Equal(oldUpdatedState.IsSunlit, originalState.IsSunlit);
        
        // Ground track should be updated
        Assert.Equal(newGroundTrack, originalState.GroundTrack);
        Assert.Single(originalState.GroundTrack);

        // Clean up
        SatelliteTrackStatePool.Return(originalState);
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(10, 50)]
    [InlineData(25, 20)]
    [InlineData(100, 10)]
    public void StressTest_MultipleOperationsProduceConsistentResults(int satelliteCount, int iterations)
    {
        // This test runs many iterations to ensure consistency under stress
        
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            // Create states using pooled approach
            var states = new List<SatelliteTrackState>();
            for (int i = 0; i < satelliteCount; i++)
            {
                states.Add(SatelliteTrackState.CreatePooled(
                    name: $"Iter{iteration}_Sat{i}",
                    noradId: $"{iteration * 1000 + i}",
                    subpoint: new GeoCoordinate(
                        (iteration + i) % 180 - 90,
                        (iteration * 2 + i) % 360 - 180,
                        400_000 + i * 1000),
                    isSunlit: (iteration + i) % 2 == 0));
            }

            // Use buffer manager
            var bufferManager = new SnapshotBufferManager();
            var snapshot1 = bufferManager.PublishDisplaySnapshot(states);
            var snapshot2 = bufferManager.PublishLiveNowSnapshot(states);

            // Use thread-local collections
            var staleBuffer = TrackingCollections.GetStaleTracksBuffer();
            for (int i = 0; i < Math.Min(5, satelliteCount); i++)
            {
                staleBuffer.Add((CreateTestSatellite($"Stale{i}"), new SatelliteVisualCache.Entry()));
            }

            // Verify results are valid
            Assert.Equal(satelliteCount, states.Count);
            Assert.Equal(satelliteCount, snapshot1.Count);
            Assert.Equal(satelliteCount, snapshot2.Count);
            Assert.True(staleBuffer.Count <= 5);

            // Return objects to pool
            SatelliteTrackStatePool.ReturnRange(states);
        }
        
        // Verify pool statistics show expected activity
        var poolStats = SatelliteTrackStatePool.GetStatistics();
        Assert.True(poolStats.RentCount >= satelliteCount * iterations);
        Assert.True(poolStats.ReturnCount >= satelliteCount * iterations);
    }

    private static void AssertSatelliteStatesEquivalent(SatelliteTrackState expected, SatelliteTrackState actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.NoradId, actual.NoradId);
        Assert.Equal(expected.Subpoint, actual.Subpoint);
        Assert.Equal(expected.LookAngles, actual.LookAngles);
        Assert.Equal(expected.AheadAzimuthDeg, actual.AheadAzimuthDeg);
        Assert.Equal(expected.MotionHeadingDeg, actual.MotionHeadingDeg);
        Assert.Equal(expected.GroundTrack, actual.GroundTrack);
        Assert.Equal(expected.NextOrbitGroundTrack, actual.NextOrbitGroundTrack);
        Assert.Equal(expected.Footprint, actual.Footprint);
        Assert.Equal(expected.FootprintRadiusDeg, actual.FootprintRadiusDeg);
        Assert.Equal(expected.IsSunlit, actual.IsSunlit);
    }

    private static StateProperties CaptureStateProperties(SatelliteTrackState state)
    {
        return new StateProperties
        {
            Name = state.Name,
            NoradId = state.NoradId,
            Subpoint = state.Subpoint,
            LookAngles = state.LookAngles,
            AheadAzimuthDeg = state.AheadAzimuthDeg,
            MotionHeadingDeg = state.MotionHeadingDeg,
            NextOrbitGroundTrack = state.NextOrbitGroundTrack,
            Footprint = state.Footprint,
            FootprintRadiusDeg = state.FootprintRadiusDeg,
            IsSunlit = state.IsSunlit
        };
    }

    private static IReadOnlyList<SatelliteTrackState> SimulateOldPublishSnapshot(IReadOnlyList<SatelliteTrackState> states)
    {
        return states.Count == 0 ? Array.Empty<SatelliteTrackState>() : states.ToArray();
    }

    private static IEnumerable<TestSatelliteData> GenerateTestSatelliteData(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return new TestSatelliteData
            {
                Name = $"TestSat{i}",
                NoradId = (12345 + i).ToString(),
                Subpoint = new GeoCoordinate(i * 5.0 % 180 - 90, i * 7.0 % 360 - 180, 400_000 + i * 1000),
                LookAngles = i % 3 == 0 ? new LookAngles(i * 10.0, i * 5.0, 1000.0 + i, i * 0.1) : null,
                MotionHeading = i % 2 == 0 ? i * 15.0 % 360 : null,
                GroundTrack = new List<GeoCoordinate> { new GeoCoordinate(i, i + 1, 0) },
                NextOrbitGroundTrack = i % 4 == 0 ? new List<GeoCoordinate> { new GeoCoordinate(i + 2, i + 3, 0) } : Array.Empty<GeoCoordinate>(),
                Footprint = new List<GeoCoordinate> { new GeoCoordinate(i - 1, i - 2, 0) },
                FootprintRadius = 5.0 + i * 0.5,
                IsSunlit = i % 2 == 0
            };
        }
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

    private sealed class TestSatelliteData
    {
        public required string Name { get; init; }
        public required string NoradId { get; init; }
        public required GeoCoordinate Subpoint { get; init; }
        public LookAngles? LookAngles { get; init; }
        public double? MotionHeading { get; init; }
        public required IReadOnlyList<GeoCoordinate> GroundTrack { get; init; }
        public required IReadOnlyList<GeoCoordinate> NextOrbitGroundTrack { get; init; }
        public required IReadOnlyList<GeoCoordinate> Footprint { get; init; }
        public double FootprintRadius { get; init; }
        public bool IsSunlit { get; init; }
    }

    private sealed class StateProperties
    {
        public required string Name { get; init; }
        public required string NoradId { get; init; }
        public required GeoCoordinate Subpoint { get; init; }
        public LookAngles? LookAngles { get; init; }
        public double? AheadAzimuthDeg { get; init; }
        public double? MotionHeadingDeg { get; init; }
        public required IReadOnlyList<GeoCoordinate> NextOrbitGroundTrack { get; init; }
        public required IReadOnlyList<GeoCoordinate> Footprint { get; init; }
        public double FootprintRadiusDeg { get; init; }
        public bool IsSunlit { get; init; }
    }
}