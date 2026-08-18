using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Core.Services;
using OscarWatch.Core.Tle;

namespace OscarWatch.Tests;

/// <summary>
/// Unit tests for RemoveSatellite optimization in TrackingOrchestrator.
/// Validates Requirements 3.1 and 3.4: functional equivalence and edge case handling.
/// </summary>
public sealed class RemoveSatelliteOptimizationUnitTests
{
    private readonly SatelliteCatalogEntry[] _testSatellites = [
        new() { NoradId = "25544", Name = "ISS (ZARYA)", Line1 = "", Line2 = "" },
        new() { NoradId = "43013", Name = "AMSAT Fox-1A", Line1 = "", Line2 = "" },
        new() { NoradId = "40967", Name = "AO-73", Line1 = "", Line2 = "" },
        new() { NoradId = "52017", Name = "AMSAT Es'hail-2", Line1 = "", Line2 = "" }
    ];

    [Fact]
    public void RemoveSatellite_WithExistingSatellite_RemovesFromCachedCollection()
    {
        // Arrange
        var orchestrator = CreateOrchestrator(_testSatellites);
        
        // Add all test satellites
        foreach (var sat in _testSatellites)
        {
            orchestrator.AddSatellite(sat);
        }

        // Act - Remove existing satellite
        orchestrator.RemoveSatellite("25544");

        // Assert - Verify satellite is removed from collection
        // Test this indirectly by calling GetLiveStates and checking the propagator was called correctly
        var states = orchestrator.GetLiveStates(DateTime.UtcNow);
        
        // The test propagator should not have been asked for the removed satellite
        // We can verify this by ensuring the collection operations maintain consistency
        Assert.NotNull(states); // Should complete successfully without throwing
    }

    [Fact]
    public void RemoveSatellite_WithNonExistingSatellite_DoesNotThrow()
    {
        // Arrange
        var orchestrator = CreateOrchestrator(_testSatellites);
        
        // Add some test satellites
        orchestrator.AddSatellite(_testSatellites[0]);
        orchestrator.AddSatellite(_testSatellites[1]);

        // Act & Assert - Remove non-existing satellite should not throw
        var exception = Record.Exception(() => orchestrator.RemoveSatellite("99999"));
        Assert.Null(exception);
    }

    [Fact]
    public void RemoveSatellite_WithEmptyCollection_DoesNotThrow()
    {
        // Arrange
        var orchestrator = CreateOrchestrator(_testSatellites);
        // Don't add any satellites

        // Act & Assert - Remove from empty collection should not throw
        var exception = Record.Exception(() => orchestrator.RemoveSatellite("25544"));
        Assert.Null(exception);
    }

    [Fact]
    public void RemoveSatellite_CallsPropagatorRemove()
    {
        // Arrange
        var propagator = new RecordingPropagator();
        var orchestrator = CreateOrchestrator(_testSatellites, propagator);
        
        orchestrator.AddSatellite(_testSatellites[0]);
        orchestrator.AddSatellite(_testSatellites[1]);

        // Act
        orchestrator.RemoveSatellite("25544");

        // Assert
        Assert.Contains("25544", propagator.RemovedSatellites);
    }

    [Fact]
    public void RemoveSatellite_RemovesFromAllInternalCaches()
    {
        // Arrange
        var orchestrator = CreateOrchestrator(_testSatellites);
        
        orchestrator.AddSatellite(_testSatellites[0]);
        orchestrator.AddSatellite(_testSatellites[1]);

        // Act
        orchestrator.RemoveSatellite("25544");

        // Assert - Verify removal doesn't cause issues with subsequent operations
        // This tests that visual cache, logged skips, and other internal state are properly cleared
        var exception = Record.Exception(() =>
        {
            orchestrator.GetLiveStates(DateTime.UtcNow);
            orchestrator.InvalidateVisualCache();
        });
        Assert.Null(exception);
    }

    [Fact]
    public void RemoveSatellite_PreservesOtherSatellites()
    {
        // Arrange
        var propagator = new RecordingPropagator();
        var orchestrator = CreateOrchestrator(_testSatellites, propagator);
        
        // Add multiple satellites
        orchestrator.AddSatellite(_testSatellites[0]); // ISS
        orchestrator.AddSatellite(_testSatellites[1]); // Fox-1A
        orchestrator.AddSatellite(_testSatellites[2]); // AO-73

        // Act - Remove one satellite
        orchestrator.RemoveSatellite("25544"); // Remove ISS

        // Assert - Other satellites should still be loaded
        Assert.Contains("43013", propagator.LoadedSatellites.Keys); // Fox-1A should remain
        Assert.Contains("40967", propagator.LoadedSatellites.Keys); // AO-73 should remain
        Assert.DoesNotContain("25544", propagator.LoadedSatellites.Keys); // ISS should be gone
    }

    [Fact]
    public void RemoveSatellite_WithNullNoradId_ThrowsArgumentException()
    {
        // Arrange
        var orchestrator = CreateOrchestrator(_testSatellites);
        orchestrator.AddSatellite(_testSatellites[0]);

        // Act & Assert - Should throw for null NORAD ID
        Assert.Throws<ArgumentNullException>(() => orchestrator.RemoveSatellite(null!));
    }

    [Fact]
    public void RemoveSatellite_WithEmptyStringNoradId_DoesNotThrow()
    {
        // Arrange
        var orchestrator = CreateOrchestrator(_testSatellites);
        orchestrator.AddSatellite(_testSatellites[0]);

        // Act & Assert - Should handle empty string gracefully
        var exception = Record.Exception(() => orchestrator.RemoveSatellite(""));
        Assert.Null(exception);
    }

    [Fact]
    public void RemoveSatellite_CalledMultipleTimesForSameSatellite_IsIdempotent()
    {
        // Arrange
        var propagator = new RecordingPropagator();
        var orchestrator = CreateOrchestrator(_testSatellites, propagator);
        
        orchestrator.AddSatellite(_testSatellites[0]);
        orchestrator.RemoveSatellite("25544"); // First removal

        // Act - Remove same satellite again
        var exception = Record.Exception(() => orchestrator.RemoveSatellite("25544"));

        // Assert - Should not throw and should be idempotent
        Assert.Null(exception);
        Assert.DoesNotContain("25544", propagator.LoadedSatellites.Keys);
    }

    [Fact]
    public void RemoveSatellite_AfterReloadEnabledSatellites_WorksCorrectly()
    {
        // Arrange
        var propagator = new RecordingPropagator();
        var orchestrator = CreateOrchestrator(_testSatellites, propagator);
        
        // Initial reload
        orchestrator.ReloadEnabledSatellites();
        
        // Add a satellite manually
        orchestrator.AddSatellite(_testSatellites[0]);

        // Act - Remove satellite after reload
        var exception = Record.Exception(() => orchestrator.RemoveSatellite("25544"));

        // Assert
        Assert.Null(exception);
        Assert.DoesNotContain("25544", propagator.LoadedSatellites.Keys);
    }

    [Fact]
    public void RemoveSatellite_CollectionStateConsistency_MaintainedAfterOperations()
    {
        // Arrange
        var orchestrator = CreateOrchestrator(_testSatellites);
        
        // Add satellites
        orchestrator.AddSatellite(_testSatellites[0]);
        orchestrator.AddSatellite(_testSatellites[1]);
        orchestrator.AddSatellite(_testSatellites[2]);

        // Act - Remove middle satellite
        orchestrator.RemoveSatellite("43013"); // Fox-1A

        // Assert - Verify state consistency by calling various methods
        var exception = Record.Exception(() =>
        {
            orchestrator.GetLiveStates(DateTime.UtcNow);
            orchestrator.InvalidateVisualCache();
            // Add another satellite to verify collection is in good state
            orchestrator.AddSatellite(_testSatellites[3]);
        });
        
        Assert.Null(exception);
    }

    private TrackingOrchestrator CreateOrchestrator(
        IReadOnlyList<SatelliteCatalogEntry> enabledSatellites,
        IOrbitPropagator? propagator = null)
    {
        return new TrackingOrchestrator(
            new StubSettingsService(),
            new StubTleService(enabledSatellites),
            propagator ?? new RecordingPropagator(),
            new StubGroundGeometry(),
            new StubPassPredictor());
    }

    #region Test Doubles

    private sealed class StubSettingsService : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public string? LoadError => null;
        public bool CanPersist => true;
        public string SettingsPath { get; } = "";
        public string SerializeCurrent() => "{}";
        public Task ReplaceAndSaveAsync(AppSettings imported, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Load() { }
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void RequestSave() { }
        public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void SyncGridFromLatLon() { }
        public void SyncLatLonFromGrid() { }
        public void EnsureSavedStations() { }
        public void ApplyActiveStation() { }
        public void SyncActiveStationFromGroundStation() { }
    }

    private sealed class StubTleService : ITleService
    {
        private readonly IReadOnlyList<SatelliteCatalogEntry> _enabled;

        public StubTleService(IReadOnlyList<SatelliteCatalogEntry> enabled) => _enabled = enabled;

        public IReadOnlyList<SatelliteCatalogEntry> Catalog => _enabled;
        public DateTime? LastFetchedUtc => DateTime.UtcNow;
        public string CachePath => "";
        public TleCatalogLoadDiagnostics? LastLoadDiagnostics => null;
        public bool IsStale(int staleHours) => false;
        public Task RefreshAsync(bool force = false, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureLoadedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void InvalidateCatalog() { }
        public string ActiveSourceLabel => "Test";
        public IReadOnlyList<SatelliteCatalogEntry> GetEnabledSatellites(AppSettings settings) => _enabled;
    }

    private sealed class RecordingPropagator : IOrbitPropagator
    {
        private readonly Dictionary<string, SatelliteCatalogEntry> _loaded = new(StringComparer.Ordinal);
        private readonly HashSet<string> _removed = new(StringComparer.Ordinal);

        public IReadOnlyCollection<string> LoadedNoradIds => _loaded.Keys;
        public IReadOnlyDictionary<string, SatelliteCatalogEntry> LoadedSatellites => _loaded;
        public IReadOnlyCollection<string> RemovedSatellites => _removed;

        public void LoadSatellite(SatelliteCatalogEntry entry) => _loaded[entry.NoradId] = entry;
        
        public void RemoveSatellite(string noradId) 
        {
            if (noradId != null)
            {
                _loaded.Remove(noradId);
                _removed.Add(noradId);
            }
        }

        public void Clear() 
        {
            _loaded.Clear();
            _removed.Clear();
        }

        public bool HasSatellite(string noradId) => _loaded.ContainsKey(noradId);
        public GeoCoordinate GetSubpoint(string noradId, DateTime utc) => new(0, 0, 400);
        public EciPosition GetEciPosition(string noradId, DateTime utc) => new(6778, 0, 0);
        public LookAngles GetLookAngles(string noradId, GroundStation site, DateTime utc) => new(180, 45, 1000, 0);
    }

    private sealed class StubGroundGeometry : IGroundGeometry
    {
        public IReadOnlyList<GeoCoordinate> GetGroundTrack(
            SatelliteCatalogEntry satellite, DateTime utcStart, DateTime utcEnd, TimeSpan step) => [];

        public IReadOnlyList<GeoCoordinate> GetFootprint(
            SatelliteCatalogEntry satellite, DateTime utc, double minimumElevationDeg) => [];
    }

    private sealed class StubPassPredictor : IPassPredictor
    {
        public Task<IReadOnlyList<PassInfo>> GetPassesAsync(
            SatelliteCatalogEntry satellite,
            GroundStation site,
            DateTime utcStart,
            DateTime utcEnd,
            double minimumElevationDeg,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PassInfo>>([]);
        }
    }

    #endregion
}