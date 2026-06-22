using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Core.Radio;

namespace OscarWatch.Tests;

public class DopplerAdaptiveThresholdTests
{
    [Theory]
    [InlineData(50, 10, 50)]
    [InlineData(50, 15, 50)]
    [InlineData(50, 25, 38)]
    [InlineData(50, 35, 25)]
    [InlineData(50, 50, 25)]
    [InlineData(50, 30, 50, false)]
    public void Resolve_scales_threshold_with_slew_rate(int baseline, double slewHzPerSec, int expected, bool enabled = true)
    {
        Assert.Equal(expected, DopplerAdaptiveThreshold.Resolve(baseline, slewHzPerSec, enabled));
    }

    [Fact]
    public void Slew_from_slope_matches_downlink_physics_order_of_magnitude()
    {
        // RS-44-class steep leg: ~0.016 km/s² at 435 MHz → ~23 Hz/s
        var slew = DopplerAdaptiveThreshold.SlewFromRangeRateSlope(435_667, 0.016);
        Assert.InRange(slew, 18, 28);
    }

    [Fact]
    public void EstimateMaxSlew_uses_uplink_centre_for_rx_fixed_strategy()
    {
        var propagator = new SlopeOnlyPropagator(0.016);
        var slew = DopplerAdaptiveThreshold.EstimateMaxSlewHzPerSec(
            propagator,
            "99999",
            new GroundStation { LatitudeDeg = 51.5, LongitudeDeg = -0.1 },
            new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc),
            rangeRateKmPerSec: -2.0,
            downlinkKHz: 435_667,
            uplinkKHz: 145_937,
            DopplerStrategy.UplinkOnly,
            beaconOnly: false);

        var expected = DopplerAdaptiveThreshold.SlewFromRangeRateSlope(145_937, 0.016);
        Assert.InRange(slew, expected - 0.5, expected + 0.5);
        Assert.True(slew < DopplerAdaptiveThreshold.SlewFromRangeRateSlope(435_667, 0.016));
    }

    private sealed class SlopeOnlyPropagator(double slopeKmPerSec2) : IOrbitPropagator
    {
        private readonly DateTime _utc = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        public void Clear() { }
        public void LoadSatellite(SatelliteCatalogEntry entry) { }
        public void RemoveSatellite(string noradId) { }
        public GeoCoordinate GetSubpoint(string noradId, DateTime utc) => new(0, 0, 400);
        public EciPosition GetEciPosition(string noradId, DateTime utc) => new(0, 0, 0);
        public bool HasSatellite(string noradId) => true;
        public IReadOnlyCollection<string> LoadedNoradIds => ["99999"];

        public LookAngles GetLookAngles(string noradId, GroundStation site, DateTime utc)
        {
            if (utc == _utc)
                return new LookAngles(180, 30, 800, -2.0);

            if (utc == _utc.AddSeconds(DopplerCatLead.RangeRateSlopeSampleSec))
                return new LookAngles(180, 30, 800, -2.0 - slopeKmPerSec2);

            return new LookAngles(180, 30, 800, -2.0);
        }
    }
}
