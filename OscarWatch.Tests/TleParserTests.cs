using OscarWatch.Core.Tle;

namespace OscarWatch.Tests;

public sealed class TleParserTests
{
    [Fact]
    public void Norad_id_extracted_from_positions_2_through_6()
    {
        // Arrange: standard three-line TLE set with NORAD ID "25544" at positions 2–6 of line 1
        var catalog = string.Join("\n",
            "ISS (ZARYA)",
            "1 25544U 98067A   24001.50000000  .00000000  00000-0  00000-0 0  9990",
            "2 25544  51.6400 100.0000 0007000  90.0000 270.0000 15.50000000100000");

        // Act
        var entries = TleParser.ParseCatalog(catalog);

        // Assert
        Assert.Single(entries);
        Assert.Equal("25544", entries[0].NoradId);
    }

    [Fact]
    public void Epoch_year_and_day_parsed_into_utc_datetime()
    {
        // Arrange: epoch field "24001.50000000" → year 2024, day 1.5 → 2024-01-01 12:00:00 UTC
        var catalog = string.Join("\n",
            "ISS (ZARYA)",
            "1 25544U 98067A   24001.50000000  .00000000  00000-0  00000-0 0  9990",
            "2 25544  51.6400 100.0000 0007000  90.0000 270.0000 15.50000000100000");

        // Act
        var entries = TleParser.ParseCatalog(catalog);

        // Assert
        Assert.Single(entries);
        Assert.NotNull(entries[0].EpochUtc);
        var expected = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expected, entries[0].EpochUtc!.Value, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Missing_line_2_skips_entry_without_exception()
    {
        // Arrange: line 1 present but no line 2 following it
        var catalog = string.Join("\n",
            "ISS (ZARYA)",
            "1 25544U 98067A   24001.50000000  .00000000  00000-0  00000-0 0  9990");

        // Act — should not throw
        var entries = TleParser.ParseCatalog(catalog);

        // Assert: entry is skipped
        Assert.Empty(entries);
    }

    [Fact]
    public void Consecutive_digit_starting_lines_not_treated_as_name()
    {
        // Arrange: two line-1s in a row (second satellite missing its name line)
        // The parser should not treat the first line 1 as the "name" for the second line 1
        var catalog = string.Join("\n",
            "SAT-A",
            "1 11111U 20001A   24001.50000000  .00000000  00000-0  00000-0 0  9991",
            "2 11111  51.6400 100.0000 0007000  90.0000 270.0000 15.50000000100001",
            "1 22222U 20002B   24001.50000000  .00000000  00000-0  00000-0 0  9992",
            "2 22222  51.6400 100.0000 0007000  90.0000 270.0000 15.50000000100002");

        // Act
        var entries = TleParser.ParseCatalog(catalog);

        // Assert: only the first entry (SAT-A) should be returned because the second
        // line 1 has a preceding line that starts with '2' (line 2 of the first satellite)
        // which is treated as a digit-starting line and excluded as a name
        Assert.Single(entries);
        Assert.Equal("11111", entries[0].NoradId);
        Assert.Equal("SAT-A", entries[0].Name);
    }

    [Fact]
    public void Line_1_shorter_than_60_characters_skipped_without_exception()
    {
        // Arrange: a short line starting with '1' that is < 60 characters
        var catalog = string.Join("\n",
            "SHORT-SAT",
            "1 99999U 24001A   24001.5",
            "2 99999  51.6400 100.0000 0007000  90.0000 270.0000 15.50000000100000");

        // Act — should not throw
        var entries = TleParser.ParseCatalog(catalog);

        // Assert: entry is skipped because line 1 is too short
        Assert.Empty(entries);
    }

    [Fact]
    public void Unparseable_epoch_field_sets_epoch_utc_to_null()
    {
        // Arrange: epoch field contains "XXXXX.XXXXXXXX" which cannot be parsed as a number
        var catalog = string.Join("\n",
            "BAD-EPOCH-SAT",
            "1 33333U 20003C   XXXXX.XXXXXXXX  .00000000  00000-0  00000-0 0  9993",
            "2 33333  51.6400 100.0000 0007000  90.0000 270.0000 15.50000000100003");

        // Act
        var entries = TleParser.ParseCatalog(catalog);

        // Assert: entry is produced but EpochUtc is null
        Assert.Single(entries);
        Assert.Null(entries[0].EpochUtc);
        Assert.Equal("33333", entries[0].NoradId);
    }
}
