using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

public class RisingPassAnnouncerTests
{
    private static SatelliteTrackState State(string noradId, string name, double elevationDeg) =>
        new()
        {
            NoradId = noradId,
            Name = name,
            Subpoint = new GeoCoordinate(0, 0),
            LookAngles = new LookAngles(180, elevationDeg, 800)
        };

    [Fact]
    public void Announces_when_rising_through_threshold()
    {
        var announcer = new RisingPassAnnouncer();
        var settings = new VoiceAnnouncementSettings { Enabled = true, AnnounceElevationDeg = -3.0 };
        var spoke = new List<string>();

        announcer.Process([State("25544", "AO-07", -5.0)], settings, spoke.Add);
        Assert.Empty(spoke);

        announcer.Process([State("25544", "AO-07", -2.0)], settings, spoke.Add);
        Assert.Single(spoke);
        Assert.Contains("rising", spoke[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Skips_when_disabled()
    {
        var announcer = new RisingPassAnnouncer();
        var settings = new VoiceAnnouncementSettings { Enabled = false, AnnounceElevationDeg = -3.0 };
        var spoke = new List<string>();

        announcer.Process([State("25544", "AO-07", -5.0)], settings, spoke.Add);
        announcer.Process([State("25544", "AO-07", -2.0)], settings, spoke.Add);

        Assert.Empty(spoke);
    }

    [Fact]
    public void Does_not_announce_when_descending_through_threshold()
    {
        var announcer = new RisingPassAnnouncer();
        var settings = new VoiceAnnouncementSettings { Enabled = true, AnnounceElevationDeg = -3.0 };
        var spoke = new List<string>();

        announcer.Process([State("25544", "AO-07", 2.0)], settings, spoke.Add);
        announcer.Process([State("25544", "AO-07", -2.0)], settings, spoke.Add);

        Assert.Empty(spoke);
    }

    [Fact]
    public void Announces_again_after_falling_below_threshold()
    {
        var announcer = new RisingPassAnnouncer();
        var settings = new VoiceAnnouncementSettings { Enabled = true, AnnounceElevationDeg = -3.0 };
        var spoke = new List<string>();

        announcer.Process([State("25544", "AO-07", -5.0)], settings, spoke.Add);
        announcer.Process([State("25544", "AO-07", -2.0)], settings, spoke.Add);
        announcer.Process([State("25544", "AO-07", -4.0)], settings, spoke.Add);
        announcer.Process([State("25544", "AO-07", -2.5)], settings, spoke.Add);

        Assert.Equal(2, spoke.Count);
    }

    [Fact]
    public void Announces_once_per_pass()
    {
        var announcer = new RisingPassAnnouncer();
        var settings = new VoiceAnnouncementSettings { Enabled = true, AnnounceElevationDeg = -3.0 };
        var spoke = new List<string>();

        announcer.Process([State("25544", "AO-07", -5.0)], settings, spoke.Add);
        announcer.Process([State("25544", "AO-07", -2.0)], settings, spoke.Add);
        announcer.Process([State("25544", "AO-07", 0.0)], settings, spoke.Add);
        announcer.Process([State("25544", "AO-07", 5.0)], settings, spoke.Add);

        Assert.Single(spoke);
    }
}
