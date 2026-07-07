using OscarWatch.Core.Models;
using OscarWatch.Core.SatelliteLink;
using OscarWatch.Core.Radio;

namespace OscarWatch.Tests;

public class SatelliteLinkMessageBuilderTests
{
    [Fact]
    public void BuildEmpty_emits_no_satellite_wisp_dde()
    {
        var msg = SatelliteLinkMessageBuilder.BuildEmpty(new DateTime(2026, 7, 7, 11, 4, 0, DateTimeKind.Utc));

        Assert.False(msg.InRange);
        Assert.Null(msg.Satellite);
        Assert.Equal("** NO SATELLITE **", msg.WispDde);
        Assert.Equal("2026-07-07T11:04:00.000Z", msg.TimestampUtc);
    }

    [Fact]
    public void Build_maps_frequencies_tracking_and_bands()
    {
        var mode = new SatelliteTransponderMode
        {
            Type = "FM VOICE",
            DownlinkKHz = 145_850,
            UplinkKHz = 435_300,
            DownlinkMode = "FM",
            UplinkMode = "FM"
        };

        var corrected = DopplerFrequencyCalculator.Compute(mode, 0, 0);
        var context = new RigTrackingContext
        {
            TrackState = new SatelliteTrackState
            {
                Name = "SO-50",
                NoradId = "27607",
                Subpoint = new GeoCoordinate(0, 0, 400),
                LookAngles = new LookAngles(91.7, 1.9, 2100.5, -4.92),
                IsSunlit = true
            },
            Mode = mode,
            Corrected = corrected,
            DopplerStrategy = DopplerStrategy.Full
        };

        var msg = SatelliteLinkMessageBuilder.Build(context, onlyWhenInRange: false, DateTime.UtcNow);

        Assert.True(msg.InRange);
        Assert.Equal("SO-50", msg.Satellite!.Name);
        Assert.Equal("27607", msg.Satellite.NoradId);
        Assert.Equal("FM VOICE", msg.Satellite.ModeType);
        Assert.Equal(435_300_000, msg.Frequencies!.UplinkHz);
        Assert.Equal(145_850_000, msg.Frequencies.DownlinkHz);
        Assert.Equal("FM", msg.Frequencies.UplinkMode);
        Assert.Equal("FM", msg.Frequencies.DownlinkMode);
        Assert.Equal("70cm", msg.Bands!.Tx);
        Assert.Equal("2m", msg.Bands.Rx);
        Assert.Equal(91.7, msg.Tracking!.AzimuthDeg, precision: 1);
        Assert.Equal(1.9, msg.Tracking.ElevationDeg, precision: 1);
        Assert.Equal("full", msg.DopplerStrategy);
        Assert.Contains("SO-50", msg.WispDde, StringComparison.Ordinal);
        Assert.Contains("AZ91,7", msg.WispDde, StringComparison.Ordinal);
        Assert.Contains("EL1,9", msg.WispDde, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_only_when_in_range_returns_empty_below_horizon()
    {
        var mode = new SatelliteTransponderMode
        {
            Type = "FM",
            DownlinkKHz = 145_850,
            UplinkKHz = 435_300,
            DownlinkMode = "FM",
            UplinkMode = "FM"
        };

        var context = new RigTrackingContext
        {
            TrackState = new SatelliteTrackState
            {
                Name = "SO-50",
                NoradId = "27607",
                Subpoint = new GeoCoordinate(0, 0, 400),
                LookAngles = new LookAngles(180, -5, 3000, 0)
            },
            Mode = mode,
            Corrected = DopplerFrequencyCalculator.Compute(mode, 0, 0)
        };

        var msg = SatelliteLinkMessageBuilder.Build(context, onlyWhenInRange: true, DateTime.UtcNow);

        Assert.False(msg.InRange);
        Assert.Equal("** NO SATELLITE **", msg.WispDde);
    }

    [Fact]
    public void PublishPolicy_skips_identical_payload_within_interval_unless_forced()
    {
        const string sig = "a|b|c";
        var now = DateTime.UtcNow;
        var posted = now.AddMilliseconds(-500);

        Assert.False(SatelliteLinkPublishPolicy.ShouldBroadcast(sig, sig, posted, now, 1000, force: false));
        Assert.True(SatelliteLinkPublishPolicy.ShouldBroadcast(sig, sig, posted, now, 1000, force: true));
        Assert.True(SatelliteLinkPublishPolicy.ShouldBroadcast(sig, "other", posted, now, 1000, force: false));
    }
}
