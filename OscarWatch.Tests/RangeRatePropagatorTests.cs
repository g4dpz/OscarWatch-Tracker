using OscarWatch.Core.Models;
using OscarWatch.Core.Radio;
using OscarWatch.Orbit;
using Zeptomoby.OrbitTools;
using SatelliteOrbit = Zeptomoby.OrbitTools.Orbit;

namespace OscarWatch.Tests;

public class RangeRatePropagatorTests
{
    private static SatelliteCatalogEntry IssEntry => new()
    {
        Name = "ISS (ZARYA)",
        NoradId = "25544",
        Line1 = "1 25544U 98067A   25205.51782528  .00016717  00000+0  10270-3 0  9993",
        Line2 = "2 25544  51.6416 247.4627 0006703 130.5360 325.0288 15.50415322908603"
    };

    private static GroundStation LondonSite => new()
    {
        LatitudeDeg = 51.5,
        LongitudeDeg = -0.1,
        AltitudeMetersAsl = 50
    };

    [Fact]
    public void Instantaneous_range_rate_is_not_one_second_range_delta()
    {
        var propagator = new PublicOrbitToolsPropagator();
        propagator.LoadSatellite(IssEntry);

        var tca = FindPassTcaUtc(propagator, IssEntry.NoradId, LondonSite);
        var orbit = new SatelliteOrbit(new Tle(IssEntry.Name, IssEntry.Line1, IssEntry.Line2));
        var groundSite = new Site(LondonSite.LatitudeDeg, LondonSite.LongitudeDeg, LondonSite.AltitudeKm);

        var sawDifference = false;
        for (var i = -300; i <= 300; i++)
        {
            var t = tca.AddSeconds(i);
            var look = propagator.GetLookAngles(IssEntry.NoradId, LondonSite, t);
            if (look.ElevationDeg < 10)
                continue;

            var topo = groundSite.GetLookAngle(orbit.PositionEci(t));
            var topoNext = groundSite.GetLookAngle(orbit.PositionEci(t.AddSeconds(1)));
            var finiteDelta = topoNext.Range - topo.Range;

            if (Math.Abs(look.RangeRateKmPerSec - finiteDelta) > 0.005)
                sawDifference = true;
        }

        Assert.True(sawDifference, "Expected measurable difference between velocity-based and 1 s Δrange.");
    }

    [Fact]
    public void Range_rate_crosses_zero_near_pass_tca()
    {
        var propagator = new PublicOrbitToolsPropagator();
        propagator.LoadSatellite(IssEntry);

        var tca = FindPassTcaUtc(propagator, IssEntry.NoradId, LondonSite);

        double? before = null;
        double? after = null;
        for (var i = -600; i <= 600; i++)
        {
            var look = propagator.GetLookAngles(IssEntry.NoradId, LondonSite, tca.AddSeconds(i));
            if (look.ElevationDeg < 10)
                continue;

            if (look.RangeRateKmPerSec < -0.01)
                before ??= look.RangeRateKmPerSec;
            else if (look.RangeRateKmPerSec > 0.01)
            {
                after = look.RangeRateKmPerSec;
                break;
            }
        }

        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.True(before < 0);
        Assert.True(after > 0);
    }

    [Fact]
    public void Doppler_shift_scales_with_instantaneous_range_rate()
    {
        var propagator = new PublicOrbitToolsPropagator();
        propagator.LoadSatellite(IssEntry);

        var tca = FindPassTcaUtc(propagator, IssEntry.NoradId, LondonSite);

        var mode = new SatelliteTransponderMode
        {
            DownlinkKHz = 435_667,
            UplinkKHz = 145_937,
            DownlinkMode = "USB",
            UplinkMode = "LSB",
            Doppler = "REV"
        };

        var rates = new List<double>();
        for (var i = -600; i <= 600; i++)
        {
            var look = propagator.GetLookAngles(IssEntry.NoradId, LondonSite, tca.AddSeconds(i));
            if (look.ElevationDeg < 10)
                continue;

            rates.Add(look.RangeRateKmPerSec);
            var corrected = DopplerFrequencyCalculator.Compute(mode, look.RangeRateKmPerSec, 0);
            Assert.Equal(
                mode.DownlinkKHz + corrected.DopplerShiftKHz,
                corrected.RadioReceiveKHz,
                6);
        }

        Assert.Contains(rates, r => r < -0.05);
        Assert.Contains(rates, r => r > 0.05);
    }

    private static DateTime FindPassTcaUtc(
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
}
