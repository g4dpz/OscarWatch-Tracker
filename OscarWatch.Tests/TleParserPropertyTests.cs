// Feature: test-coverage-expansion, Property 1: TLE Catalog Round-Trip

using FsCheck.Xunit;
using OscarWatch.Core.Models;
using OscarWatch.Core.Tle;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 1.1**
///
/// Property-based tests verifying that serialising a TLE catalog with
/// <see cref="TleParser.SerializeCatalog"/> and parsing it back with
/// <see cref="TleParser.ParseCatalog"/> yields identical entries
/// (Name, NoradId, Line1, Line2).
/// </summary>
public class TleParserPropertyTests
{
    // Valid characters for a satellite name (alpha only to avoid trim issues with leading/trailing whitespace)
    private const string NameChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>
    /// Property 1: TLE Catalog Round-Trip.
    ///
    /// For any list of valid SatelliteCatalogEntry objects, serialising with SerializeCatalog
    /// then parsing with ParseCatalog produces an equivalent list where each entry has the
    /// same Name, NoradId, Line1, and Line2.
    /// </summary>
    [Property]
    public bool Round_trip_parse_serialize_parse_preserves_entries(int countRaw, int noradSeed, int nameSeed)
    {
        // Constrain entry count to 1–5 (avoid trivial empty case)
        var count = 1 + Math.Abs(countRaw % 5);

        var entries = new List<SatelliteCatalogEntry>();
        for (var i = 0; i < count; i++)
        {
            // Generate a NORAD ID: 5-digit number (00001–99999)
            var noradNum = 1 + Math.Abs((noradSeed + i * 7919) % 99999);
            var noradId = noradNum.ToString("D5");

            // Generate a satellite name that doesn't start with '1' or '2'
            var nameIdx = Math.Abs((nameSeed + i * 6151) % NameChars.Length);
            var name = $"{NameChars[nameIdx]}SAT-{noradId}";

            // Build a valid TLE line 1 (69 characters, starts with '1', NORAD at positions 2-6)
            var line1 = BuildLine1(noradId, i);

            // Build a valid TLE line 2 (69 characters, starts with '2', NORAD at positions 2-6)
            var line2 = BuildLine2(noradId, i);

            entries.Add(new SatelliteCatalogEntry
            {
                Name = name,
                NoradId = noradId,
                Line1 = line1,
                Line2 = line2,
                EpochUtc = null
            });
        }

        // Serialize then parse
        var serialized = TleParser.SerializeCatalog(entries);
        var parsed = TleParser.ParseCatalog(serialized);

        // Verify round-trip preserves all entries
        if (parsed.Count != entries.Count)
            return false;

        for (var i = 0; i < entries.Count; i++)
        {
            if (parsed[i].Name != entries[i].Name)
                return false;
            if (parsed[i].NoradId != entries[i].NoradId)
                return false;
            if (parsed[i].Line1 != entries[i].Line1)
                return false;
            if (parsed[i].Line2 != entries[i].Line2)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Builds a valid 69-character TLE line 1.
    /// Format matches the standard three-line TLE specification.
    /// </summary>
    private static string BuildLine1(string noradId, int variant)
    {
        // "1 NNNNNC LLLLLLLL YYDDD.DDDDDDDD  .DDDDDDDD  DDDDD-D  DDDDD-D D NNNNG"
        // Exactly 69 characters with proper field widths
        var checksum = variant % 10;
        return $"1 {noradId}U 24001A   24001.50000000  .00000000  00000-0  00000-0 0  999{checksum}";
    }

    /// <summary>
    /// Builds a valid 69-character TLE line 2.
    /// Format matches the standard three-line TLE specification.
    /// </summary>
    private static string BuildLine2(string noradId, int variant)
    {
        // "2 NNNNN III.IIII RRR.RRRR EEEEEEE PPP.PPPP NNN.NNNN MM.MMMMMMMMMNNNNnc"
        // Exactly 69 characters with proper field widths
        var revNum = (10000 + variant) % 100000;
        var checksum = variant % 10;
        return $"2 {noradId}  51.6400 100.0000 0007000  90.0000 270.0000 15.50000000{revNum:D5}{checksum}";
    }
}
