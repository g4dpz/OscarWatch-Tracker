using OscarWatch.Core.Geo;

namespace OscarWatch.Tests;

public sealed class GpsdJsonParserTests
{
    [Fact]
    public void TryParseTpvLine_parses_fix_with_mode_3()
    {
        const string line =
            """{"class":"TPV","device":"/dev/ttyUSB0","time":"2024-06-01T12:34:56.000Z","lat":51.5,"lon":-0.12,"alt":42.0,"mode":3}""";

        Assert.True(GpsdJsonParser.TryParseTpvLine(line, out var fix));
        Assert.Equal(51.5, fix.LatitudeDeg);
        Assert.Equal(-0.12, fix.LongitudeDeg);
        Assert.Equal(42.0, fix.AltitudeMeters);
        Assert.Equal(3, fix.Mode);
        Assert.True(fix.HasValidPosition);
        Assert.Equal(new DateTime(2024, 6, 1, 12, 34, 56, DateTimeKind.Utc), fix.UtcTime);
    }

    [Fact]
    public void TryParseTpvLine_rejects_no_fix_mode()
    {
        const string line = """{"class":"TPV","mode":1,"lat":51.5,"lon":-0.12}""";

        Assert.True(GpsdJsonParser.TryParseTpvLine(line, out var fix));
        Assert.False(fix.HasValidPosition);
    }

    [Fact]
    public void TryParseSkyLine_counts_used_satellites()
    {
        const string line =
            """{"class":"SKY","satellites":[{"used":true},{"used":false},{"used":true}]}""";

        Assert.True(GpsdJsonParser.TryParseSkyLine(line, out var count));
        Assert.Equal(2, count);
    }

    [Fact]
    public void TryParseTpvLine_ignores_non_tpv_messages()
    {
        const string line = """{"class":"VERSION","release":"3.25"}""";

        Assert.False(GpsdJsonParser.TryParseTpvLine(line, out _));
    }
}
