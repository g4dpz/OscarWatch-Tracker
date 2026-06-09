using OscarWatch.Core.Cloudlog;

namespace OscarWatch.Tests;

public class CloudlogRadioPublishPolicyTests
{
    private const string SigA = "FO-29|145952650|435850450|LSB|USB";
    private const string SigB = "FO-29|145952651|435850450|LSB|USB";

    [Fact]
    public void ShouldPost_when_signature_changes()
    {
        var now = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(CloudlogRadioPublishPolicy.ShouldPost(null, SigA, now, now, 600_000));
        Assert.True(CloudlogRadioPublishPolicy.ShouldPost(SigA, SigB, now, now.AddSeconds(1), 600_000));
    }

    [Fact]
    public void ShouldNotPost_when_unchanged_before_keepalive()
    {
        var posted = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var now = posted.AddMinutes(9);
        Assert.False(CloudlogRadioPublishPolicy.ShouldPost(SigA, SigA, posted, now, 600_000));
    }

    [Fact]
    public void ShouldPost_keepalive_after_interval()
    {
        var posted = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var now = posted.AddMinutes(10);
        Assert.True(CloudlogRadioPublishPolicy.ShouldPost(SigA, SigA, posted, now, 600_000));
    }

    [Fact]
    public void Migrate_maps_legacy_1000ms_to_ten_minutes()
    {
        Assert.Equal(600_000, CloudlogRadioPublishPolicy.MigrateKeepaliveIntervalMs(1000));
        Assert.Equal(600_000, CloudlogRadioPublishPolicy.MigrateKeepaliveIntervalMs(0));
        Assert.Equal(300_000, CloudlogRadioPublishPolicy.MigrateKeepaliveIntervalMs(300_000));
    }
}
