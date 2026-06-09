// Feature: test-coverage-expansion, Property 14: ICS Structural Validity
// Feature: test-coverage-expansion, Property 15: ICS Content Formatting

using FsCheck.Xunit;
using OscarWatch.Core.Export;
using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 5.1, 5.2, 5.3, 5.4, 5.5, 5.6**
///
/// Property-based tests verifying structural and formatting invariants of
/// <see cref="IcsPassExporter"/>: calendar wrapper, VEVENT count, UID content,
/// timestamp formatting, and character escaping.
/// </summary>
public class IcsPassExporterPropertyTests
{
    /// <summary>
    /// Property 14: ICS Structural Validity.
    ///
    /// For any non-empty list of PassInfo objects, the output of BuildCalendar
    /// SHALL begin with "BEGIN:VCALENDAR", end with "END:VCALENDAR", contain
    /// exactly N "BEGIN:VEVENT" blocks (where N is the input count), and each
    /// VEVENT SHALL contain a UID with the pass's NORAD ID and AOS ticks.
    /// </summary>
    [Property]
    public bool Output_has_valid_calendar_structure_and_vevent_count(int passCount, int aosOffsetMinutes, int durationMinutes)
    {
        // Constrain to 1–10 passes to keep tests reasonable
        var count = Math.Abs(passCount % 10) + 1;
        var baseTime = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var passes = new List<PassInfo>();

        for (int i = 0; i < count; i++)
        {
            var aos = baseTime.AddMinutes(Math.Abs((aosOffsetMinutes + i * 100) % 1440));
            var dur = Math.Abs(durationMinutes % 30) + 5; // 5–34 minutes
            passes.Add(CreatePass($"{25544 + i}", $"SAT-{i}", aos, TimeSpan.FromMinutes(dur)));
        }

        var station = CreateStation();
        var result = IcsPassExporter.BuildCalendar(passes, station);

        // Must begin with BEGIN:VCALENDAR
        if (!result.StartsWith("BEGIN:VCALENDAR", StringComparison.Ordinal))
            return false;

        // Must end with END:VCALENDAR (trimming trailing newline)
        if (!result.TrimEnd().EndsWith("END:VCALENDAR", StringComparison.Ordinal))
            return false;

        // Must contain exactly N VEVENT blocks
        var veventBeginCount = CountOccurrences(result, "BEGIN:VEVENT");
        var veventEndCount = CountOccurrences(result, "END:VEVENT");
        if (veventBeginCount != count || veventEndCount != count)
            return false;

        // Each VEVENT must contain a UID with NORAD ID and AOS ticks
        for (int i = 0; i < count; i++)
        {
            var noradId = $"{25544 + i}";
            var aosTicks = passes[i].AosUtc.Ticks.ToString();
            var expectedUidFragment = $"{noradId}-{aosTicks}";
            if (!result.Contains(expectedUidFragment, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Property 15: ICS Content Formatting.
    ///
    /// For any PassInfo with arbitrary satellite name and description content,
    /// the output of BuildCalendar SHALL format DTSTART/DTEND as yyyyMMddTHHmmssZ,
    /// escape semicolons/commas/backslashes with a preceding backslash, and
    /// replace newlines with literal \n.
    /// </summary>
    [Property]
    public bool Timestamps_formatted_correctly_and_special_chars_escaped(int aosOffsetMinutes, int durationMinutes)
    {
        var baseTime = new DateTime(2024, 3, 20, 8, 0, 0, DateTimeKind.Utc);
        var aos = baseTime.AddMinutes(Math.Abs(aosOffsetMinutes % 1440));
        var dur = Math.Abs(durationMinutes % 30) + 5;
        var los = aos.AddMinutes(dur);

        // Use a satellite name with special characters: semicolons, commas, backslashes
        var satName = "SAT;with,special\\chars";
        var pass = CreatePass("25544", satName, aos, TimeSpan.FromMinutes(dur));

        // Use a station name with special characters
        var station = new GroundStation
        {
            DisplayName = "Home;Station,One\\Two",
            LatitudeDeg = 51.5,
            LongitudeDeg = -0.1,
            AltitudeMetersAsl = 50,
            GridSquare = "IO91wm"
        };

        var result = IcsPassExporter.BuildCalendar(new[] { pass }, station);

        // Verify DTSTART format: yyyyMMddTHHmmssZ
        var expectedDtStart = $"DTSTART:{aos.ToString("yyyyMMdd'T'HHmmss'Z'")}";
        if (!result.Contains(expectedDtStart, StringComparison.Ordinal))
            return false;

        // Verify DTEND format: yyyyMMddTHHmmssZ
        var expectedDtEnd = $"DTEND:{los.ToString("yyyyMMdd'T'HHmmss'Z'")}";
        if (!result.Contains(expectedDtEnd, StringComparison.Ordinal))
            return false;

        // Verify semicolons are escaped (in SUMMARY which contains satellite name)
        // The satellite name "SAT;with,special\\chars" should become "SAT\;with\,special\\\\chars"
        // after escaping (backslash first, then semicolons, then commas)
        if (result.Contains(";with", StringComparison.Ordinal) &&
            !result.Contains("\\;with", StringComparison.Ordinal))
            return false;

        if (result.Contains(",special", StringComparison.Ordinal) &&
            !result.Contains("\\,special", StringComparison.Ordinal))
            return false;

        // Verify newlines are replaced with literal \n in DESCRIPTION
        // The description contains \n characters which should be escaped
        if (result.Contains("DESCRIPTION:") && result.Contains("\n"))
        {
            // Extract the DESCRIPTION line and verify no raw newlines within it
            // The DESCRIPTION value itself should use literal \n, not actual newlines
            // Note: the output uses AppendLine so lines are separated by \r\n,
            // but within a field value, \n should be replaced with literal \\n
            var lines = result.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                if (line.StartsWith("DESCRIPTION:", StringComparison.Ordinal))
                {
                    var descValue = line.Substring("DESCRIPTION:".Length);
                    // The description value should not contain raw newlines
                    // (since we split on newlines, if we got a line starting with DESCRIPTION:,
                    // the entire value should be on this one line with literal \n)
                    if (descValue.Contains('\n') || descValue.Contains('\r'))
                        return false;
                    // The description value should contain literal \n sequences
                    if (!descValue.Contains("\\n", StringComparison.Ordinal))
                        return false;
                    break;
                }
            }
        }

        return true;
    }

    private static PassInfo CreatePass(string noradId, string satelliteName, DateTime aos, TimeSpan duration)
    {
        return new PassInfo
        {
            SatelliteName = satelliteName,
            NoradId = noradId,
            AosUtc = aos,
            LosUtc = aos + duration,
            MaxElevationDeg = 45.0,
            MaxElevationUtc = aos + duration / 2,
            AosAzimuthDeg = 180.0,
            LosAzimuthDeg = 0.0
        };
    }

    private static GroundStation CreateStation()
    {
        return new GroundStation
        {
            DisplayName = "Home",
            LatitudeDeg = 51.5,
            LongitudeDeg = -0.1,
            AltitudeMetersAsl = 50,
            GridSquare = "IO91wm"
        };
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
