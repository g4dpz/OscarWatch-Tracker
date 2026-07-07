using OscarWatch.Core.Tle;

namespace OscarWatch.Tests;

public sealed class TleLineFormatterTests
{
    [Fact]
    public void FormatInternationalDesignator_converts_object_id()
    {
        Assert.Equal("74089B  ", TleLineFormatter.FormatInternationalDesignator("1974-089B"));
        Assert.Equal("93061C  ", TleLineFormatter.FormatInternationalDesignator("1993-061C"));
    }

    [Fact]
    public void FormatScientificField_matches_norad_style()
    {
        Assert.Equal(" 00000-0", TleLineFormatter.FormatScientificField(0));
        Assert.Equal("-48931-3", TleLineFormatter.FormatScientificField(-4.8931e-4));
    }

    [Fact]
    public void FormatEpochField_uses_year_and_day_of_year()
    {
        var epoch = new DateTime(2026, 5, 21, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal("26141.50000000", TleLineFormatter.FormatEpochField(epoch));
    }

    [Fact]
    public void Formatted_lines_are_69_characters_with_checksum()
    {
        var record = new GpElementRecord
        {
            AmsatName = "AO-07",
            ObjectId = "1974-089B",
            NoradCatId = 7530,
            Epoch = "2026-07-07T12:21:17.710848",
            Inclination = 101.9901,
            Eccentricity = 0.00126647,
            RaOfAscNode = 201.9731,
            ArgOfPericenter = 92.559,
            MeanAnomaly = 74.3678,
            MeanMotion = 12.53698425,
            MeanMotionDot = -4.6e-07,
            MeanMotionDdot = 0,
            Bstar = 4.948808e-06,
            EphemerisType = 0,
            ClassificationType = "U",
            ElementSetNo = 999,
            RevAtEpoch = 36306
        };

        Assert.True(GpJsonCatalogParser.TryParseEpoch(record.Epoch, out var epochUtc));
        var (line1, line2) = TleLineFormatter.FormatLines(record, epochUtc);

        Assert.Equal(69, line1.Length);
        Assert.Equal(69, line2.Length);
        Assert.Equal(TleLineFormatter.ComputeChecksum(line1[..68]), line1[68] - '0');
        Assert.Equal(TleLineFormatter.ComputeChecksum(line2[..68]), line2[68] - '0');
    }
}
