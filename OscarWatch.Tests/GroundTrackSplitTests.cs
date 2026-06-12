using OscarWatch.Controls;
using OscarWatch.Core.Geo;
using OscarWatch.Core.Models;
using OscarWatch.Core.Orbit;
using OscarWatch.Orbit;

namespace OscarWatch.Tests;

public class GroundTrackSplitTests
{
    private static readonly SatelliteCatalogEntry Ao07 = new()
    {
        Name = "AO-07",
        NoradId = "07530",
        Line1 = "1 07530U 74089B   26141.31992461 -.00000054  00000-0 -48931-4 0  9992",
        Line2 = "2 07530 101.9910 154.2858 0012269 180.6108 191.1977 12.53697584357151"
    };

    private static readonly SatelliteCatalogEntry Rs44 = new()
    {
        Name = "RS-44",
        NoradId = "44909",
        Line1 = "1 44909U 19096E   26141.11069286  .00000018  00000-0  30335-4 0  9995",
        Line2 = "2 44909  82.5230 357.7010 0216952 207.4466 151.5042 12.79748393298881"
    };
    [Fact]
    public void SelectGroundTrackWrapOffset_prefers_primary_map_for_wide_chain()
    {
        const double w = 1200;
        var chain = new List<(double X, double Y)>
        {
            (100, 200),
            (500, 220),
            (1000, 240)
        };

        Assert.Equal(0, WorldMapControl.SelectGroundTrackWrapOffset(chain, w));
    }

    [Fact]
    public void SelectGroundTrackWrapOffset_skips_tiny_edge_stub()
    {
        const double w = 1200;
        var chain = new List<(double X, double Y)>
        {
            (-1180, 520),
            (-1150, 525)
        };

        Assert.Null(WorldMapControl.SelectGroundTrackWrapOffset(chain, w));
    }

    [Fact]
    public void SplitForMapDraw_keeps_two_point_segments()
    {
        var points = new[]
        {
            new GeoCoordinate(0, 0, 400),
            new GeoCoordinate(10, 20, 400),
        };

        var chains = EquirectangularProjection.SplitForMapDraw(points, 360, 180);

        Assert.Single(chains);
        Assert.Equal(2, chains[0].Count);
    }

    [Fact]
    public void SplitForMapDraw_keeps_short_tail_after_pixel_jump_break()
    {
        var points = new List<GeoCoordinate>();
        for (var lat = 0.0; lat <= 20; lat += 2)
            points.Add(new GeoCoordinate(lat, 10, 400));

        // Large latitude jump in pixel space forces a chain break (|Δy| > height/3).
        points.Add(new GeoCoordinate(85, 12, 400));
        points.Add(new GeoCoordinate(86, 13, 400));

        var chains = EquirectangularProjection.SplitForMapDraw(points, 360, 180);

        Assert.True(chains.Count >= 2);
        Assert.Contains(chains, c => c.Count == 2);
    }

    [Fact]
    public void Ao07_polar_ground_track_uses_fewer_chains_than_pixel_jump_split()
    {
        var propagator = new PublicOrbitToolsPropagator();
        propagator.LoadSatellite(Ao07);
        var geometry = new SampledGroundGeometry(propagator);

        var utc = new DateTime(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);
        var periodMin = 1440.0 / 12.53697584357151;
        var half = TimeSpan.FromMinutes(periodMin / 2.0);
        var track = geometry.GetGroundTrack(Ao07, utc - half, utc + half, TimeSpan.FromSeconds(60));

        Assert.True(track.Count >= 50);

        const double w = 1200;
        const double h = 600;
        var groundTrack = EquirectangularProjection.ProjectGroundTrackForDraw(track, w, h);

        Assert.True(groundTrack.Count <= 4,
            $"Expected only antimeridian splits for AO-07, got {groundTrack.Count}.");
        Assert.True(groundTrack.Sum(c => c.Count) >= track.Count);

        var drawableSegments = 0;
        foreach (var chain in groundTrack)
        {
            for (var i = 0; i < chain.Count - 1; i++)
            {
                var dx = Math.Abs(chain[i + 1].X - chain[i].X);
                var dy = Math.Abs(chain[i + 1].Y - chain[i].Y);
                if (dx <= w / 2.0 && dy <= h / 3.0)
                    drawableSegments++;
            }
        }

        Assert.True(drawableSegments >= track.Count - 4,
            $"Expected a mostly continuous AO-07 track ({drawableSegments} drawable segments).");
    }

    [Fact]
    public void Rs44_high_inclination_track_does_not_draw_rim_chords_at_pole()
    {
        var propagator = new PublicOrbitToolsPropagator();
        propagator.LoadSatellite(Rs44);
        var geometry = new SampledGroundGeometry(propagator);

        const double w = 1200;
        const double h = 600;
        var maxDy = h / 3.0;
        var baseUtc = new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc);
        var periodMin = 1440.0 / 12.79748393298881;
        var half = TimeSpan.FromMinutes(periodMin / 2.0);

        for (var hour = 0; hour < 24; hour++)
        {
            var utc = baseUtc.AddHours(hour);
            var track = geometry.GetGroundTrack(Rs44, utc - half, utc + half, TimeSpan.FromSeconds(60));
            if (track.Count < 50)
                continue;

            var chains = EquirectangularProjection.ProjectGroundTrackForDraw(track, w, h);
            foreach (var chain in chains)
            {
                for (var i = 0; i < chain.Count - 1; i++)
                {
                    var p0 = chain[i];
                    var p1 = chain[i + 1];
                    var dx = Math.Abs(p1.X - p0.X);
                    var dy = Math.Abs(p1.Y - p0.Y);
                    Assert.True(dy <= maxDy + 0.01,
                        $"At UTC {utc:HH:mm}, unexpected vertical jump of {dy:F1}px.");

                    // Segments drawable by WorldMapControl use maxDx = w/2.
                    var drawable = dx <= w / 2.0 && dy <= maxDy;
                    Assert.False(drawable && p0.Y <= h * 0.08 && p1.Y <= h * 0.08 && dx > w / 4.0,
                        $"At UTC {utc:HH:mm}, drawable high-lat chord dx={dx:F1} y0={p0.Y:F1} y1={p1.Y:F1}.");
                }
            }
        }
    }
}
