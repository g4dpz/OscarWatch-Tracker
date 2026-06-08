using OscarWatch.Core.Geo;

namespace OscarWatch.Tests;

public sealed class NmeaSentenceParserTests
{
    [Fact]
    public void ParseGga_extracts_position_altitude_and_satellites()
    {
        const string sentence = "$GPGGA,092750.000,5321.6802,N,00630.3372,W,1,08,1.03,61.7,M,55.2,M,,*76";

        Assert.True(NmeaSentenceParser.TryParseGga(sentence, out var fix));
        Assert.Equal(53.361337, fix.LatitudeDeg!.Value, 4);
        Assert.Equal(-6.505620, fix.LongitudeDeg!.Value, 4);
        Assert.Equal(61.7, fix.AltitudeMeters!.Value, 1);
        Assert.Equal(8, fix.SatellitesInUse);
        Assert.Equal(1, fix.FixQuality);
        Assert.True(fix.HasValidPosition);
    }

    [Fact]
    public void ParseRmc_extracts_position_and_utc()
    {
        const string sentence = "$GPRMC,092750.000,A,5321.6802,N,00630.3372,W,0.02,31.66,050623,,,A*45";

        Assert.True(NmeaSentenceParser.TryParseRmc(sentence, out var fix));
        Assert.Equal(53.361337, fix.LatitudeDeg!.Value, 4);
        Assert.Equal(-6.505620, fix.LongitudeDeg!.Value, 4);
        Assert.NotNull(fix.UtcTime);
        Assert.Equal(9, fix.UtcTime!.Value.Hour);
        Assert.Equal(27, fix.UtcTime.Value.Minute);
        Assert.Equal(50, fix.UtcTime.Value.Second);
        Assert.Equal(2023, fix.UtcTime.Value.Year);
        Assert.True(fix.HasValidPosition);
    }

    [Fact]
    public void ParseLine_accepts_gn_multi_constellation_prefix()
    {
        const string sentence = "$GNGGA,123519,4807.038,N,01131.000,E,1,08,0.9,545.4,M,46.9,M,,*47";

        Assert.True(NmeaSentenceParser.TryParseLine(sentence, out var fix));
        Assert.True(fix.HasValidPosition);
        Assert.Equal(48.1173, fix.LatitudeDeg!.Value, 3);
        Assert.Equal(11.51667, fix.LongitudeDeg!.Value, 3);
    }

    [Fact]
    public void ParseGga_rejects_invalid_fix_quality()
    {
        const string sentence = "$GPGGA,092750.000,5321.6802,N,00630.3372,W,0,08,1.03,61.7,M,55.2,M,,*47";

        Assert.True(NmeaSentenceParser.TryParseGga(sentence, out var fix));
        Assert.False(fix.HasValidPosition);
    }
}
