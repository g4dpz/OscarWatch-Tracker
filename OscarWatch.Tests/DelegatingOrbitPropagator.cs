using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;

namespace OscarWatch.Tests;

/// <summary>Delegates to an inner propagator, optionally overriding look-angle results for tests.</summary>
internal sealed class DelegatingOrbitPropagator : IOrbitPropagator
{
    private readonly IOrbitPropagator _inner;

    public Func<string, GroundStation, DateTime, LookAngles>? GetLookAnglesHandler { get; init; }

    public DelegatingOrbitPropagator(IOrbitPropagator inner) => _inner = inner;

    public void LoadSatellite(SatelliteCatalogEntry entry) => _inner.LoadSatellite(entry);

    public void RemoveSatellite(string noradId) => _inner.RemoveSatellite(noradId);

    public void Clear() => _inner.Clear();

    public GeoCoordinate GetSubpoint(string noradId, DateTime utc) => _inner.GetSubpoint(noradId, utc);

    public EciPosition GetEciPosition(string noradId, DateTime utc) => _inner.GetEciPosition(noradId, utc);

    public LookAngles GetLookAngles(string noradId, GroundStation site, DateTime utc) =>
        GetLookAnglesHandler?.Invoke(noradId, site, utc)
        ?? _inner.GetLookAngles(noradId, site, utc);

    public bool HasSatellite(string noradId) => _inner.HasSatellite(noradId);

    public IReadOnlyList<string> LoadedNoradIds => _inner.LoadedNoradIds;
}
