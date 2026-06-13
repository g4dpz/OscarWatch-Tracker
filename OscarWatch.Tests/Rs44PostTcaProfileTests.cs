using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Core.Radio;
using OscarWatch.Orbit;

namespace OscarWatch.Tests;

public class Rs44PostTcaProfileTests
{
    [Fact]
    public void Rs44_late_post_tca_los_leg_keeps_receding_floor_blend()
    {
        var entry = Rs44();
        var propagator = new PublicOrbitToolsPropagator();
        propagator.LoadSatellite(entry);
        var site = new GroundStation { LatitudeDeg = 51.5, LongitudeDeg = -0.1, AltitudeMetersAsl = 50 };
        var (tca, maxEl) = FindTca(propagator, entry.NoradId, site, 60);
        var settings = new RigSettings { DopplerCatLeadEnabled = true, CatDelayMs = 100 };

        foreach (var offset in new[] { 210, 300, 420 })
        {
            var utc = tca.AddSeconds(offset);
            var look = propagator.GetLookAngles(entry.NoradId, site, utc);
            if (look.ElevationDeg < 5)
                continue;

            var state = new SatelliteTrackState
            {
                Name = entry.Name,
                NoradId = entry.NoradId,
                Subpoint = propagator.GetSubpoint(entry.NoradId, utc),
                LookAngles = look
            };
            var slope = DopplerCatLead.ComputeRangeRateSlopeKmPerSec2(
                propagator, entry.NoradId, site, utc, look.RangeRateKmPerSec);
            var lead = DopplerCatLead.ResolveRangeRates(propagator, settings, site, state, utc);

            Assert.True(maxEl >= 60);
            Assert.True(look.RangeRateKmPerSec > 0, $"offset {offset}s: expected receding range rate.");
            Assert.True(lead.LeadBlend >= 0.45,
                $"RS-44 offset {offset}s: blend {lead.LeadBlend:F3}, slope {slope:F4}, el {look.ElevationDeg:F1}°.");
        }
    }

    private static SatelliteCatalogEntry Rs44() => new()
    {
        Name = "RS-44",
        NoradId = "44909",
        Line1 = "1 44909U 19096E   26141.11069286  .00000018  00000-0  30335-4 0  9995",
        Line2 = "2 44909  82.5230 357.7010 0216952 207.4466 151.5042 12.79748393298881"
    };

    private static (DateTime Tca, double MaxEl) FindTca(
        PublicOrbitToolsPropagator propagator, string noradId, GroundStation site, double minEl)
    {
        var start = new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc);
        var bestEl = double.MinValue;
        var bestUtc = start;
        for (var i = 0; i < 259_200; i += 15)
        {
            var t = start.AddSeconds(i);
            var look = propagator.GetLookAngles(noradId, site, t);
            if (look.ElevationDeg > bestEl)
            {
                bestEl = look.ElevationDeg;
                bestUtc = t;
            }
        }

        Assert.True(bestEl >= minEl, $"Expected pass el ≥ {minEl}° (best {bestEl:F1}°).");
        return (bestUtc, bestEl);
    }
}
