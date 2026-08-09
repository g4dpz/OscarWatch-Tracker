using OscarWatch.Core.Display;

namespace OscarWatch.Tests;

public sealed class PassUtcTests
{
    [Fact]
    public void Normalize_treats_unspecified_as_utc_without_offset_shift()
    {
        var raw = new DateTime(2026, 8, 9, 12, 40, 0, DateTimeKind.Unspecified);
        var normalized = PassUtc.Normalize(raw);

        Assert.Equal(DateTimeKind.Utc, normalized.Kind);
        Assert.Equal(12, normalized.Hour);
        Assert.Equal(40, normalized.Minute);
    }

    [Fact]
    public void Normalize_converts_local_to_utc()
    {
        var local = new DateTime(2026, 8, 9, 12, 40, 0, DateTimeKind.Local);
        var normalized = PassUtc.Normalize(local);

        Assert.Equal(DateTimeKind.Utc, normalized.Kind);
        Assert.Equal(local.ToUniversalTime(), normalized);
    }
}
