using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Core.Radio;
using OscarWatch.Orbit;

namespace OscarWatch.Tests;

public class DopplerCatLeadTests
{
    private static readonly GroundStation Site = new()
    {
        LatitudeDeg = 51.5,
        LongitudeDeg = -0.1,
        AltitudeMetersAsl = 50
    };

    private static SatelliteTrackState StateWithRate(double rangeRateKmPerSec) => new()
    {
        Name = "TEST",
        NoradId = "99999",
        Subpoint = new GeoCoordinate(0, 0, 400),
        LookAngles = new LookAngles(180, 30, 800, rangeRateKmPerSec)
    };

    [Fact]
    public void Disabled_returns_snapshot_range_rate()
    {
        var propagator = new StubPropagator(1.0, 2.0);
        var settings = new RigSettings { DopplerCatLeadEnabled = false, CatDelayMs = 100 };

        var (rx, tx) = DopplerCatLead.ResolveRangeRates(
            propagator,
            settings,
            Site,
            StateWithRate(-3.5),
            DateTime.UtcNow);

        Assert.Equal(-3.5, rx);
        Assert.Equal(-3.5, tx);
        Assert.Equal(0, propagator.CallCount);
    }

    [Fact]
    public void Enabled_queries_propagator_at_half_cat_delay_per_leg()
    {
        var utc = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var propagator = new StubPropagator(1.0, 2.0);
        var settings = new RigSettings
        {
            DopplerCatLeadEnabled = true,
            CatDelayMs = 100
        };
        var state = StateWithRate(-3.5);

        var (rx, tx) = DopplerCatLead.ResolveRangeRates(propagator, settings, Site, state, utc);

        Assert.Equal(2, propagator.CallCount);
        Assert.Equal(utc.AddMilliseconds(50), propagator.LastRxUtc);
        Assert.Equal(utc.AddMilliseconds(50), propagator.LastTxUtc);
        Assert.Equal(1.0, rx);
        Assert.Equal(2.0, tx);
    }

    [Fact]
    public void Dual_radio_uses_per_leg_cat_delay()
    {
        var utc = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var propagator = new StubPropagator(1.0, 2.0);
        var settings = new RigSettings
        {
            DopplerCatLeadEnabled = true,
            DualRadioEnabled = true,
            Downlink = new RigEndpointSettings { CatDelayMs = 80 },
            Uplink = new RigEndpointSettings { CatDelayMs = 120 }
        };

        DopplerCatLead.ResolveRangeRates(propagator, settings, Site, StateWithRate(0), utc);

        Assert.Equal(utc.AddMilliseconds(40), propagator.LastRxUtc);
        Assert.Equal(utc.AddMilliseconds(60), propagator.LastTxUtc);
    }

    [Fact]
    public void Propagation_failure_falls_back_to_snapshot_rate()
    {
        var propagator = new ThrowingPropagator();
        var settings = new RigSettings { DopplerCatLeadEnabled = true, CatDelayMs = 100 };

        var (rx, tx) = DopplerCatLead.ResolveRangeRates(
            propagator,
            settings,
            Site,
            StateWithRate(-2.2),
            DateTime.UtcNow);

        Assert.Equal(-2.2, rx);
        Assert.Equal(-2.2, tx);
    }

    [Fact]
    public void Lead_produces_different_doppler_than_now_on_iss_pass_segment()
    {
        var entry = new SatelliteCatalogEntry
        {
            Name = "ISS (ZARYA)",
            NoradId = "25544",
            Line1 = "1 25544U 98067A   25205.51782528  .00016717  00000+0  10270-3 0  9993",
            Line2 = "2 25544  51.6416 247.4627 0006703 130.5360 325.0288 15.50415322908603"
        };
        var propagator = new PublicOrbitToolsPropagator();
        propagator.LoadSatellite(entry);

        var site = new GroundStation { LatitudeDeg = 51.5, LongitudeDeg = -0.1, AltitudeMetersAsl = 50 };
        var tca = FindIssPassTcaUtc(propagator, entry.NoradId, site);
        var utc = tca.AddSeconds(120);
        var state = new SatelliteTrackState
        {
            Name = entry.Name,
            NoradId = entry.NoradId,
            Subpoint = propagator.GetSubpoint(entry.NoradId, utc),
            LookAngles = propagator.GetLookAngles(entry.NoradId, site, utc)
        };

        var mode = new SatelliteTransponderMode
        {
            DownlinkKHz = 435_667,
            UplinkKHz = 145_937,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "NOR"
        };

        var nowRate = state.LookAngles!.RangeRateKmPerSec;
        var nowCorrected = DopplerFrequencyCalculator.Compute(mode, nowRate, 0);

        var settings = new RigSettings { DopplerCatLeadEnabled = true, CatDelayMs = 100 };
        var (rxLead, _) = DopplerCatLead.ResolveRangeRates(propagator, settings, site, state, utc);
        var leadCorrected = DopplerFrequencyCalculator.Compute(mode, rxLead, 0);

        Assert.NotEqual(nowRate, rxLead);
        Assert.NotEqual(nowCorrected.RadioReceiveKHz, leadCorrected.RadioReceiveKHz);
    }

    [Fact]
    public void Null_propagator_returns_snapshot_rate()
    {
        var settings = new RigSettings { DopplerCatLeadEnabled = true, CatDelayMs = 100 };

        var (rx, tx) = DopplerCatLead.ResolveRangeRates(
            null,
            settings,
            Site,
            StateWithRate(4.1),
            DateTime.UtcNow);

        Assert.Equal(4.1, rx);
        Assert.Equal(4.1, tx);
    }

    private sealed class StubPropagator(double rxRate, double txRate) : IOrbitPropagator
    {
        public int CallCount { get; private set; }
        public DateTime LastRxUtc { get; private set; }
        public DateTime LastTxUtc { get; private set; }

        public void Clear() { }
        public void LoadSatellite(SatelliteCatalogEntry entry) { }
        public void RemoveSatellite(string noradId) { }
        public GeoCoordinate GetSubpoint(string noradId, DateTime utc) => new(0, 0, 400);
        public EciPosition GetEciPosition(string noradId, DateTime utc) => new(0, 0, 0);
        public bool HasSatellite(string noradId) => true;
        public IReadOnlyCollection<string> LoadedNoradIds => ["99999"];

        public LookAngles GetLookAngles(string noradId, GroundStation site, DateTime utc)
        {
            CallCount++;
            if (CallCount == 1)
            {
                LastRxUtc = utc;
                return new LookAngles(180, 30, 800, rxRate);
            }

            LastTxUtc = utc;
            return new LookAngles(180, 30, 800, txRate);
        }
    }

    private static DateTime FindIssPassTcaUtc(
        PublicOrbitToolsPropagator propagator,
        string noradId,
        GroundStation site)
    {
        var start = new DateTime(2026, 5, 23, 0, 0, 0, DateTimeKind.Utc);
        var bestEl = double.MinValue;
        var bestUtc = start;

        for (var i = 0; i < 86_400; i += 15)
        {
            var t = start.AddSeconds(i);
            var look = propagator.GetLookAngles(noradId, site, t);
            if (look.ElevationDeg > bestEl)
            {
                bestEl = look.ElevationDeg;
                bestUtc = t;
            }
        }

        Assert.True(bestEl >= 15, $"Expected a usable ISS pass over the test site (best el {bestEl:F1}°).");
        return bestUtc;
    }

    private sealed class ThrowingPropagator : IOrbitPropagator
    {
        public void Clear() { }
        public void LoadSatellite(SatelliteCatalogEntry entry) { }
        public void RemoveSatellite(string noradId) { }
        public GeoCoordinate GetSubpoint(string noradId, DateTime utc) => throw new InvalidOperationException();
        public EciPosition GetEciPosition(string noradId, DateTime utc) => throw new InvalidOperationException();
        public bool HasSatellite(string noradId) => true;
        public IReadOnlyCollection<string> LoadedNoradIds => [];
        public LookAngles GetLookAngles(string noradId, GroundStation site, DateTime utc) =>
            throw new InvalidOperationException();
    }
}
