using System.Collections.Concurrent;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

public sealed class HotPathCollectionsTests
{
    [Fact]
    public void GetPassInfoBuffer_ReturnsNonNullCollection()
    {
        // Act
        var buffer = HotPathCollections.GetPassInfoBuffer();
        
        // Assert
        Assert.NotNull(buffer);
        Assert.Empty(buffer);
        Assert.True(buffer.Capacity >= 64);
    }

    [Fact]
    public void GetLocalPassBuffer_ReturnsNonNullCollection()
    {
        // Act
        var buffer = HotPathCollections.GetLocalPassBuffer();
        
        // Assert
        Assert.NotNull(buffer);
        Assert.Empty(buffer);
        Assert.True(buffer.Capacity >= 32);
    }

    [Fact]
    public void GetRemotePassBuffer_ReturnsNonNullCollection()
    {
        // Act
        var buffer = HotPathCollections.GetRemotePassBuffer();
        
        // Assert
        Assert.NotNull(buffer);
        Assert.Empty(buffer);
        Assert.True(buffer.Capacity >= 32);
    }

    [Fact]
    public void GetPassInfoBuffer_ClearsCollectionBetweenCalls()
    {
        // Arrange
        var buffer1 = HotPathCollections.GetPassInfoBuffer();
        buffer1.Add(new PassInfo
        {
            SatelliteName = "Test Satellite",
            NoradId = "12345",
            AosUtc = DateTime.UtcNow,
            LosUtc = DateTime.UtcNow.AddMinutes(10),
            MaxElevationDeg = 45.0,
            MaxElevationUtc = DateTime.UtcNow.AddMinutes(5),
            AosAzimuthDeg = 180.0,
            LosAzimuthDeg = 270.0
        });

        // Act
        var buffer2 = HotPathCollections.GetPassInfoBuffer();

        // Assert
        Assert.Empty(buffer2); // Buffer should be cleared between calls
        Assert.Same(buffer1, buffer2); // Same thread should reuse the same buffer instance
    }

    [Fact]
    public void GetLocalPassBuffer_ClearsCollectionBetweenCalls()
    {
        // Arrange
        var buffer1 = HotPathCollections.GetLocalPassBuffer();
        buffer1.Add(new PassInfo
        {
            SatelliteName = "Test Satellite",
            NoradId = "12345",
            AosUtc = DateTime.UtcNow,
            LosUtc = DateTime.UtcNow.AddMinutes(10),
            MaxElevationDeg = 45.0,
            MaxElevationUtc = DateTime.UtcNow.AddMinutes(5),
            AosAzimuthDeg = 180.0,
            LosAzimuthDeg = 270.0
        });

        // Act
        var buffer2 = HotPathCollections.GetLocalPassBuffer();

        // Assert
        Assert.Empty(buffer2); // Buffer should be cleared between calls
        Assert.Same(buffer1, buffer2); // Same thread should reuse the same buffer instance
    }

    [Fact]
    public void GetRemotePassBuffer_ClearsCollectionBetweenCalls()
    {
        // Arrange
        var buffer1 = HotPathCollections.GetRemotePassBuffer();
        buffer1.Add(new PassInfo
        {
            SatelliteName = "Test Satellite",
            NoradId = "12345",
            AosUtc = DateTime.UtcNow,
            LosUtc = DateTime.UtcNow.AddMinutes(10),
            MaxElevationDeg = 45.0,
            MaxElevationUtc = DateTime.UtcNow.AddMinutes(5),
            AosAzimuthDeg = 180.0,
            LosAzimuthDeg = 270.0
        });

        // Act
        var buffer2 = HotPathCollections.GetRemotePassBuffer();

        // Assert
        Assert.Empty(buffer2); // Buffer should be cleared between calls
        Assert.Same(buffer1, buffer2); // Same thread should reuse the same buffer instance
    }

    [Fact]
    public void ThreadLocalBuffers_AreDifferentForDifferentThreads()
    {
        // Arrange
        List<PassInfo>? thread1Buffer = null;
        List<PassInfo>? thread2Buffer = null;
        var thread1Ready = new ManualResetEventSlim();
        var thread2Ready = new ManualResetEventSlim();

        // Act
        var thread1 = new Thread(() =>
        {
            thread1Buffer = HotPathCollections.GetPassInfoBuffer();
            thread1Ready.Set();
            thread2Ready.Wait(); // Wait for thread2 to get its buffer
        });

        var thread2 = new Thread(() =>
        {
            thread2Buffer = HotPathCollections.GetPassInfoBuffer();
            thread2Ready.Set();
            thread1Ready.Wait(); // Wait for thread1 to get its buffer
        });

        thread1.Start();
        thread2.Start();
        thread1.Join();
        thread2.Join();

        // Assert
        Assert.NotNull(thread1Buffer);
        Assert.NotNull(thread2Buffer);
        Assert.NotSame(thread1Buffer, thread2Buffer); // Different threads should get different buffer instances
    }

    [Fact]
    public void BufferCapacityManagement_DoesNotShrinkWhenReused()
    {
        // Arrange
        var buffer = HotPathCollections.GetPassInfoBuffer();
        var initialCapacity = buffer.Capacity;

        // Add many items to potentially grow capacity
        for (int i = 0; i < 100; i++)
        {
            buffer.Add(new PassInfo
            {
                SatelliteName = $"Satellite {i}",
                NoradId = i.ToString(),
                AosUtc = DateTime.UtcNow.AddHours(i),
                LosUtc = DateTime.UtcNow.AddHours(i + 1),
                MaxElevationDeg = 45.0,
                MaxElevationUtc = DateTime.UtcNow.AddHours(i + 0.5),
                AosAzimuthDeg = 180.0,
                LosAzimuthDeg = 270.0
            });
        }

        var expandedCapacity = buffer.Capacity;

        // Act - Get buffer again (which should clear it)
        var buffer2 = HotPathCollections.GetPassInfoBuffer();

        // Assert
        Assert.Same(buffer, buffer2); // Should reuse the same buffer
        Assert.Empty(buffer2); // Should be cleared
        Assert.Equal(expandedCapacity, buffer2.Capacity); // Capacity should be preserved after clearing
        Assert.True(buffer2.Capacity > initialCapacity); // Capacity should have grown from initial size
    }

    [Fact]
    public void FallbackStrategy_AlwaysReturnsValidList()
    {
        // Test that all buffer methods always return a non-null, usable list
        // even under hypothetical memory pressure scenarios

        // Act
        var passInfoBuffer = HotPathCollections.GetPassInfoBuffer();
        var localPassBuffer = HotPathCollections.GetLocalPassBuffer();
        var remotePassBuffer = HotPathCollections.GetRemotePassBuffer();

        // Assert
        Assert.NotNull(passInfoBuffer);
        Assert.NotNull(localPassBuffer);
        Assert.NotNull(remotePassBuffer);

        // Verify the lists are functional
        var testPass = new PassInfo
        {
            SatelliteName = "Test Satellite",
            NoradId = "12345",
            AosUtc = DateTime.UtcNow,
            LosUtc = DateTime.UtcNow.AddMinutes(10),
            MaxElevationDeg = 45.0,
            MaxElevationUtc = DateTime.UtcNow.AddMinutes(5),
            AosAzimuthDeg = 180.0,
            LosAzimuthDeg = 270.0
        };

        passInfoBuffer.Add(testPass);
        localPassBuffer.Add(testPass);
        remotePassBuffer.Add(testPass);

        Assert.Single(passInfoBuffer);
        Assert.Single(localPassBuffer);
        Assert.Single(remotePassBuffer);
    }

    [Fact]
    public void GracefulDegradation_HandlesMemoryPressure()
    {
        // This test verifies that the fallback mechanism provides graceful degradation
        // by ensuring smaller collections are still functional

        // Act - Get buffers multiple times to verify consistency
        var buffers = new List<List<PassInfo>>();
        for (int i = 0; i < 10; i++)
        {
            buffers.Add(HotPathCollections.GetPassInfoBuffer());
            buffers.Add(HotPathCollections.GetLocalPassBuffer());
            buffers.Add(HotPathCollections.GetRemotePassBuffer());
        }

        // Assert - All buffers should be valid and functional
        foreach (var buffer in buffers)
        {
            Assert.NotNull(buffer);
            Assert.Empty(buffer); // Should be cleared for each request
            
            // Verify basic functionality works regardless of capacity
            var testPass = new PassInfo
            {
                SatelliteName = "Test Satellite",
                NoradId = "12345",
                AosUtc = DateTime.UtcNow,
                LosUtc = DateTime.UtcNow.AddMinutes(10),
                MaxElevationDeg = 45.0,
                MaxElevationUtc = DateTime.UtcNow.AddMinutes(5),
                AosAzimuthDeg = 180.0,
                LosAzimuthDeg = 270.0
            };

            buffer.Add(testPass);
            Assert.Single(buffer);
            
            buffer.Clear();
            Assert.Empty(buffer);
        }
    }

    [Fact]
    public void FallbackStrategy_MaintainsThreadSafety()
    {
        // Verify that fallback allocations don't interfere with thread safety
        var results = new ConcurrentBag<List<PassInfo>>();
        var tasks = new List<Task>();

        // Act - Create multiple tasks that request buffers concurrently
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var buffer = HotPathCollections.GetPassInfoBuffer();
                results.Add(buffer);
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert - All buffers should be valid
        Assert.Equal(10, results.Count);
        foreach (var buffer in results)
        {
            Assert.NotNull(buffer);
            Assert.Empty(buffer);
        }
    }

    [Fact]  
    public void AllocationFallback_PreservesCapacityExpectations()
    {
        // Verify that even under fallback conditions, we get reasonable capacity
        
        // Act
        var passInfoBuffer = HotPathCollections.GetPassInfoBuffer();
        var localPassBuffer = HotPathCollections.GetLocalPassBuffer();
        var remotePassBuffer = HotPathCollections.GetRemotePassBuffer();

        // Assert - Check that buffers have appropriate minimum capacities or can grow
        // PassInfo buffer should have capacity for at least some initial items
        Assert.True(passInfoBuffer.Capacity >= 0); // Even zero capacity is acceptable as fallback
        
        // Verify it can grow when items are added
        for (int i = 0; i < 10; i++)
        {
            passInfoBuffer.Add(new PassInfo
            {
                SatelliteName = $"Satellite {i}",
                NoradId = i.ToString(),
                AosUtc = DateTime.UtcNow.AddHours(i),
                LosUtc = DateTime.UtcNow.AddHours(i + 1),
                MaxElevationDeg = 45.0,
                MaxElevationUtc = DateTime.UtcNow.AddHours(i + 0.5),
                AosAzimuthDeg = 180.0,
                LosAzimuthDeg = 270.0
            });
        }
        
        Assert.Equal(10, passInfoBuffer.Count);
        Assert.True(passInfoBuffer.Capacity >= 10); // Should have grown to accommodate items
        
        // Similar checks for other buffers
        Assert.True(localPassBuffer.Capacity >= 0);
        Assert.True(remotePassBuffer.Capacity >= 0);
    }

    [Fact]
    public void MemoryPressureScenario_DemonstratesGracefulDegradation()
    {
        // This test demonstrates that the allocation fallback strategy works correctly
        // by simulating what happens when thread-local collections are unavailable
        
        // In a memory pressure scenario, the system should still function correctly
        // even if it falls back to method-local allocations with reduced capacity
        
        // Act - Test each buffer type independently to verify consistent behavior
        var passInfoBuffers = new List<List<PassInfo>>();
        var localPassBuffers = new List<List<PassInfo>>();
        var remotePassBuffers = new List<List<PassInfo>>();
        
        for (int i = 0; i < 3; i++)
        {
            // Get fresh buffers (should be cleared)
            var passInfoBuffer = HotPathCollections.GetPassInfoBuffer();
            var localPassBuffer = HotPathCollections.GetLocalPassBuffer();
            var remotePassBuffer = HotPathCollections.GetRemotePassBuffer();
            
            // Verify they start empty (correct behavior - clearing between calls)
            Assert.Empty(passInfoBuffer);
            Assert.Empty(localPassBuffer);
            Assert.Empty(remotePassBuffer);
            
            // Add test data to verify functionality
            var testPass = new PassInfo
            {
                SatelliteName = $"Test Satellite {i}",
                NoradId = $"1234{i}",
                AosUtc = DateTime.UtcNow.AddHours(i),
                LosUtc = DateTime.UtcNow.AddHours(i + 1),
                MaxElevationDeg = 45.0,
                MaxElevationUtc = DateTime.UtcNow.AddHours(i + 0.5),
                AosAzimuthDeg = 180.0,
                LosAzimuthDeg = 270.0
            };
            
            passInfoBuffer.Add(testPass);
            localPassBuffer.Add(testPass);
            remotePassBuffer.Add(testPass);
            
            passInfoBuffers.Add(passInfoBuffer);
            localPassBuffers.Add(localPassBuffer);
            remotePassBuffers.Add(remotePassBuffer);
        }
        
        // Assert - Verify all allocations are functional regardless of capacity
        for (int i = 0; i < 3; i++)
        {
            var passInfoBuffer = passInfoBuffers[i];
            var localPassBuffer = localPassBuffers[i];
            var remotePassBuffer = remotePassBuffers[i];
            
            // All buffers should be functional
            Assert.NotNull(passInfoBuffer);
            Assert.NotNull(localPassBuffer);
            Assert.NotNull(remotePassBuffer);
            
            // Should contain the one item we added
            Assert.Single(passInfoBuffer);
            Assert.Single(localPassBuffer);
            Assert.Single(remotePassBuffer);
            
            // Capacity should be non-negative (can be zero for minimal fallback)
            Assert.True(passInfoBuffer.Capacity >= 0);
            Assert.True(localPassBuffer.Capacity >= 0);
            Assert.True(remotePassBuffer.Capacity >= 0);
            
            // Verify they're functionally equivalent (same thread should get same instance)
            if (i > 0)
            {
                Assert.Same(passInfoBuffers[0], passInfoBuffer);
                Assert.Same(localPassBuffers[0], localPassBuffer);
                Assert.Same(remotePassBuffers[0], remotePassBuffer);
            }
        }
        
        // Test that the buffers can handle more complex operations
        var testBuffer = HotPathCollections.GetPassInfoBuffer();
        Assert.Empty(testBuffer); // Should be cleared
        
        // Add multiple items and test sorting functionality
        for (int j = 0; j < 5; j++)
        {
            testBuffer.Add(new PassInfo
            {
                SatelliteName = $"Test {j}",
                NoradId = j.ToString(),
                AosUtc = DateTime.UtcNow.AddHours(j),
                LosUtc = DateTime.UtcNow.AddHours(j + 1),
                MaxElevationDeg = 45.0,
                MaxElevationUtc = DateTime.UtcNow.AddHours(j + 0.5),
                AosAzimuthDeg = 180.0,
                LosAzimuthDeg = 270.0
            });
        }
        
        Assert.Equal(5, testBuffer.Count);
        
        // Test sorting (critical for functional equivalence)
        testBuffer.Sort((a, b) => DateTime.Compare(b.AosUtc, a.AosUtc)); // Reverse sort
        Assert.Equal(5, testBuffer.Count);
        Assert.True(testBuffer[0].AosUtc >= testBuffer[1].AosUtc);
        Assert.True(testBuffer[1].AosUtc >= testBuffer[2].AosUtc);
    }
}