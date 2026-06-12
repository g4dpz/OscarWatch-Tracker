using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Core.Radio;

namespace OscarWatch.Tests;

/// <summary>
/// Validates Requirement 6: DopplerCatLead equal-lead short-circuit.
/// When rxLeadMs == txLeadMs, the propagator is called once (not twice) for lead resolution.
/// </summary>
public class DopplerCatLeadShortCircuitTests
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

    /// <summary>
    /// Validates: Requirements 6.1
    /// When rxLeadMs == txLeadMs and both > 0, propagator is called once for lead (plus once for slope).
    /// Single-radio setup: CatDelayMs = 100 → rxLeadMs = txLeadMs = 50 ms.
    /// </summary>
    [Fact]
    public void Equal_leads_calls_propagator_once_for_lead()
    {
        var utc = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var propagator = new CallCountingPropagator(utc, slopeRate: -2.0, leadRate: 5.5);
        var settings = new RigSettings
        {
            DopplerCatLeadEnabled = true,
            CatDelayMs = 100 // rxLeadMs = txLeadMs = 50 ms
        };
        var state = StateWithRate(-3.5);

        DopplerCatLead.ResolveRangeRates(propagator, settings, Site, state, utc);

        // 1 call for slope sample + 1 call for shared lead = 2 total
        Assert.Equal(2, propagator.TotalCallCount);
        Assert.Equal(1, propagator.LeadCallCount);
    }

    /// <summary>
    /// Validates: Requirements 6.2
    /// When rxLeadMs != txLeadMs, propagator is called separately for each lead (two lead calls).
    /// Dual-radio setup with different CAT delays.
    /// </summary>
    [Fact]
    public void Different_leads_calls_propagator_twice_for_lead()
    {
        var utc = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var propagator = new CallCountingPropagator(utc, slopeRate: -2.0, leadRate: 5.5);
        var settings = new RigSettings
        {
            DopplerCatLeadEnabled = true,
            DualRadioEnabled = true,
            Downlink = new RigEndpointSettings { CatDelayMs = 80 },  // rxLeadMs = 40
            Uplink = new RigEndpointSettings { CatDelayMs = 120 }    // txLeadMs = 50
        };
        var state = StateWithRate(-3.5);

        DopplerCatLead.ResolveRangeRates(propagator, settings, Site, state, utc);

        // 1 call for slope sample + 2 calls for separate leads = 3 total
        Assert.Equal(3, propagator.TotalCallCount);
        Assert.Equal(2, propagator.LeadCallCount);
    }

    /// <summary>
    /// Validates: Requirements 6.3
    /// When rxLeadMs == txLeadMs, the short-circuit produces identical DopplerLeadRangeRates
    /// as the two-call path would: rx and tx range rates are both equal to the blended rate.
    /// </summary>
    [Fact]
    public void Equal_leads_produces_identical_rx_and_tx_rates()
    {
        var utc = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        const double snapshotRate = -3.5;
        const double leadRate = 1.0;
        var propagator = new CallCountingPropagator(utc, slopeRate: -2.0, leadRate: leadRate);
        var settings = new RigSettings
        {
            DopplerCatLeadEnabled = true,
            CatDelayMs = 100 // equal leads
        };
        var state = StateWithRate(snapshotRate);

        var result = DopplerCatLead.ResolveRangeRates(propagator, settings, Site, state, utc);

        // With equal leads, rx and tx should be identical
        Assert.Equal(result.RxRangeRateKmPerSec, result.TxRangeRateKmPerSec);

        // The result should be Lerp(snapshot, leadRate, blend) where blend = 1.0 (steep slope)
        // slope = |slopeRate - snapshotRate| / 1.0 = |-2.0 - (-3.5)| / 1.0 = 1.5
        // That's well above SteepRangeRateSlopeKmPerSec2 (0.018), so blend = 1.0
        // Lerp(-3.5, 1.0, 1.0) = 1.0
        Assert.Equal(leadRate, result.RxRangeRateKmPerSec);
        Assert.Equal(leadRate, result.TxRangeRateKmPerSec);
        Assert.Equal(1.0, result.LeadBlend);
    }

    /// <summary>
    /// Validates: Requirements 6.3
    /// Output equivalence: simulate what the two-call path would produce for equal leads
    /// and confirm the short-circuit gives the same answer.
    /// </summary>
    [Fact]
    public void Short_circuit_output_matches_two_call_path_for_equal_leads()
    {
        var utc = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        const double snapshotRate = -3.0;
        const double leadRate = 2.0;

        // The two-call path would call propagator at utc+50ms for rx and at utc+50ms for tx,
        // getting the same rate both times. The short-circuit calls once and reuses.
        // Both should yield identical results.
        var propagator = new CallCountingPropagator(utc, slopeRate: -1.5, leadRate: leadRate);
        var settings = new RigSettings
        {
            DopplerCatLeadEnabled = true,
            CatDelayMs = 100 // rxLeadMs = txLeadMs = 50 ms
        };
        var state = StateWithRate(snapshotRate);

        var result = DopplerCatLead.ResolveRangeRates(propagator, settings, Site, state, utc);

        // slope = |-1.5 - (-3.0)| / 1.0 = 1.5, well above 0.018 → blend = 1.0
        // Lerp(-3.0, 2.0, 1.0) = 2.0 for both
        Assert.Equal(result.RxRangeRateKmPerSec, result.TxRangeRateKmPerSec);
        Assert.Equal(leadRate, result.RxRangeRateKmPerSec);

        // In the two-call path (rxLeadMs == txLeadMs), both calls would return the same rate,
        // so the blended rx and tx would be identical — matching short-circuit output.
    }

    /// <summary>
    /// Validates: Requirements 6.3
    /// For different leads, rx and tx may differ when the propagator returns different rates.
    /// </summary>
    [Fact]
    public void Different_leads_can_produce_different_rx_and_tx_rates()
    {
        var utc = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        const double snapshotRate = 0.0;

        // Dual-radio: Downlink.CatDelayMs = 80 → rxLeadMs = 40, Uplink.CatDelayMs = 120 → txLeadMs = 50
        // The propagator returns different rates for different lead times
        var propagator = new DifferentialLeadPropagator(utc, slopeRate: 0.2, rxRate: 1.5, txRate: 2.5);
        var settings = new RigSettings
        {
            DopplerCatLeadEnabled = true,
            DualRadioEnabled = true,
            Downlink = new RigEndpointSettings { CatDelayMs = 80 },  // rxLeadMs = 40
            Uplink = new RigEndpointSettings { CatDelayMs = 120 }    // txLeadMs = 50
        };
        var state = StateWithRate(snapshotRate);

        var result = DopplerCatLead.ResolveRangeRates(propagator, settings, Site, state, utc);

        // slope = |0.2 - 0| / 1.0 = 0.2, well above 0.018 → blend = 1.0
        // Lerp(0, 1.5, 1.0) = 1.5 for rx, Lerp(0, 2.5, 1.0) = 2.5 for tx
        Assert.NotEqual(result.RxRangeRateKmPerSec, result.TxRangeRateKmPerSec);
        Assert.Equal(1.5, result.RxRangeRateKmPerSec);
        Assert.Equal(2.5, result.TxRangeRateKmPerSec);
    }

    /// <summary>
    /// Validates: Requirements 6.1
    /// Verifies that the short-circuit uses the correct lead time (rxLeadMs) for its single call.
    /// </summary>
    [Fact]
    public void Equal_leads_queries_propagator_at_correct_utc_offset()
    {
        var utc = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var propagator = new CallCountingPropagator(utc, slopeRate: -2.0, leadRate: 5.5);
        var settings = new RigSettings
        {
            DopplerCatLeadEnabled = true,
            CatDelayMs = 100 // ResolveLeadMs(100) = 50
        };
        var state = StateWithRate(-3.5);

        DopplerCatLead.ResolveRangeRates(propagator, settings, Site, state, utc);

        // The short-circuit should query at utc + 50 ms
        Assert.Equal(utc.AddMilliseconds(50), propagator.LastLeadUtc);
    }

    /// <summary>
    /// A propagator that counts total calls and distinguishes slope calls from lead calls.
    /// Returns a steep slope so the lead path is always exercised.
    /// </summary>
    private sealed class CallCountingPropagator(
        DateTime resolveUtc,
        double slopeRate,
        double leadRate) : IOrbitPropagator
    {
        public int TotalCallCount { get; private set; }
        public int LeadCallCount { get; private set; }
        public DateTime LastLeadUtc { get; private set; }

        public void Clear() { }
        public void LoadSatellite(SatelliteCatalogEntry entry) { }
        public void RemoveSatellite(string noradId) { }
        public GeoCoordinate GetSubpoint(string noradId, DateTime utc) => new(0, 0, 400);
        public EciPosition GetEciPosition(string noradId, DateTime utc) => new(0, 0, 0);
        public bool HasSatellite(string noradId) => true;
        public IReadOnlyCollection<string> LoadedNoradIds => ["99999"];

        public LookAngles GetLookAngles(string noradId, GroundStation site, DateTime utc)
        {
            TotalCallCount++;

            // Slope sample: utc + 1 second
            if (utc == resolveUtc.AddSeconds(DopplerCatLead.RangeRateSlopeSampleSec))
                return new LookAngles(180, 30, 800, slopeRate);

            // Any other call is a lead call
            LeadCallCount++;
            LastLeadUtc = utc;
            return new LookAngles(180, 30, 800, leadRate);
        }
    }

    /// <summary>
    /// A propagator that returns different rates for different lead times,
    /// used to verify the two-call path produces distinct rx/tx values.
    /// </summary>
    private sealed class DifferentialLeadPropagator(
        DateTime resolveUtc,
        double slopeRate,
        double rxRate,
        double txRate) : IOrbitPropagator
    {
        public void Clear() { }
        public void LoadSatellite(SatelliteCatalogEntry entry) { }
        public void RemoveSatellite(string noradId) { }
        public GeoCoordinate GetSubpoint(string noradId, DateTime utc) => new(0, 0, 400);
        public EciPosition GetEciPosition(string noradId, DateTime utc) => new(0, 0, 0);
        public bool HasSatellite(string noradId) => true;
        public IReadOnlyCollection<string> LoadedNoradIds => ["99999"];

        public LookAngles GetLookAngles(string noradId, GroundStation site, DateTime utc)
        {
            // Slope sample: utc + 1 second
            if (utc == resolveUtc.AddSeconds(DopplerCatLead.RangeRateSlopeSampleSec))
                return new LookAngles(180, 30, 800, slopeRate);

            // rx lead: utc + 40 ms (ResolveLeadMs(80) = 40)
            if (utc == resolveUtc.AddMilliseconds(40))
                return new LookAngles(180, 30, 800, rxRate);

            // tx lead: utc + 50 ms (ResolveLeadMs(120) = 50)
            if (utc == resolveUtc.AddMilliseconds(50))
                return new LookAngles(180, 30, 800, txRate);

            return new LookAngles(180, 30, 800, 0);
        }
    }
}
