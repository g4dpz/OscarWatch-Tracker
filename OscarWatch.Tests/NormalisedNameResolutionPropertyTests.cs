// Feature: performance-optimisations, Property 6: Normalised name resolution equivalence

using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 6.1, 6.2, 6.3**
///
/// Property-based tests verifying that the O(1) normalised-name dictionary lookup
/// returns the same result as an O(n) foreach scan over all keys with NormalizeName comparison.
/// </summary>
public class NormalisedNameResolutionPropertyTests
{
    /// <summary>
    /// Replicates the NormalizeName logic from SatelliteDatabaseService (private static).
    /// </summary>
    private static string NormalizeName(string name) =>
        name.Replace(" ", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .ToUpperInvariant();

    /// <summary>
    /// O(n) reference implementation: iterate all keys in order, normalize each,
    /// and return the first entry whose normalized key matches the normalized query.
    /// </summary>
    private static string? LinearScanResolve(
        IReadOnlyList<KeyValuePair<string, string>> entries,
        string query)
    {
        var normalizedQuery = NormalizeName(query);
        foreach (var (key, value) in entries)
        {
            if (string.Equals(NormalizeName(key), normalizedQuery, StringComparison.Ordinal))
                return value;
        }
        return null;
    }

    /// <summary>
    /// O(1) dictionary implementation: build a dictionary using TryAdd (first-wins),
    /// then perform a single lookup.
    /// </summary>
    private static string? DictionaryResolve(
        IReadOnlyList<KeyValuePair<string, string>> entries,
        string query)
    {
        var dict = new Dictionary<string, string>(entries.Count, StringComparer.Ordinal);
        foreach (var (key, value) in entries)
            dict.TryAdd(NormalizeName(key), value);

        return dict.GetValueOrDefault(NormalizeName(query));
    }

    /// <summary>
    /// Generates a satellite name with spaces, hyphens, and mixed case.
    /// Uses byte seeds to build deterministic but varied names.
    /// </summary>
    private static string GenerateSatelliteName(byte[] seeds, int index)
    {
        var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var separators = new[] { ' ', '-', ' ', '-' };
        var seed = (seeds is not null && index < seeds.Length) ? seeds[index] : (byte)(index * 7);

        var length = (seed % 12) + 3; // 3–14 chars
        var result = new char[length];
        for (var i = 0; i < length; i++)
        {
            var charSeed = (byte)((seed + i * 13) % 256);
            var choice = charSeed % 30;
            if (choice < 26)
            {
                var c = chars[choice];
                // Mix case based on position and seed
                result[i] = (charSeed % 3 == 0) ? char.ToLowerInvariant(c) : c;
            }
            else
            {
                // Insert separator (space or hyphen)
                result[i] = separators[choice % separators.Length];
            }
        }

        return new string(result);
    }

    /// <summary>
    /// Property 6: Normalised name resolution equivalence.
    ///
    /// For any set of satellite names (containing spaces, hyphens, mixed case)
    /// and any query string, the O(1) dictionary lookup returns the same result
    /// as the O(n) foreach loop reference.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Dictionary_lookup_matches_linear_scan(
        byte entryCountByte,
        byte[] nameSeeds,
        byte querySeed)
    {
        // Generate 5–50 entries
        var entryCount = (entryCountByte % 46) + 5;
        var entries = new List<KeyValuePair<string, string>>(entryCount);

        for (var i = 0; i < entryCount; i++)
        {
            var name = GenerateSatelliteName(nameSeeds, i);
            var value = $"Entry_{i}";
            entries.Add(new KeyValuePair<string, string>(name, value));
        }

        // Generate a query string — sometimes pick from existing names (to test hits),
        // sometimes generate a new one (to test misses)
        string query;
        var strategy = querySeed % 4;
        if (strategy < 2 && entries.Count > 0)
        {
            // Use an existing name (potentially with case/separator variations)
            var sourceEntry = entries[querySeed % entries.Count].Key;
            query = strategy == 0
                ? sourceEntry                          // exact match
                : sourceEntry.ToLowerInvariant();       // case variation
        }
        else
        {
            // Generate a random query (likely a miss)
            query = GenerateSatelliteName(
                nameSeeds is not null && nameSeeds.Length > 0
                    ? [(byte)(nameSeeds[0] ^ querySeed)]
                    : [querySeed],
                querySeed);
        }

        var linearResult = LinearScanResolve(entries, query);
        var dictResult = DictionaryResolve(entries, query);

        return linearResult == dictResult;
    }

    /// <summary>
    /// Property 6: First-wins semantics preserved on collisions.
    ///
    /// When multiple entries normalize to the same key, both the dictionary and
    /// the linear scan return the same (first) entry.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool First_wins_on_normalized_collisions(byte baseSeed)
    {
        // Deliberately create entries that collide after normalization
        // e.g., "AO-7", "AO 7", "ao-7" all normalize to "AO7"
        var baseName = GenerateSatelliteName([baseSeed], 0);
        var entries = new List<KeyValuePair<string, string>>
        {
            new(baseName, "First"),
            new(baseName.Replace(" ", "-"), "Second"),
            new(baseName.ToLowerInvariant(), "Third"),
            new(baseName.ToUpperInvariant().Replace("-", " "), "Fourth")
        };

        var query = baseName;

        var linearResult = LinearScanResolve(entries, query);
        var dictResult = DictionaryResolve(entries, query);

        return linearResult == dictResult;
    }

    /// <summary>
    /// Property 6: Empty or whitespace-only queries resolve consistently.
    /// Both approaches should find no match or the same match.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool Empty_and_whitespace_queries_resolve_consistently(byte entryCountByte, byte[] nameSeeds)
    {
        var entryCount = (entryCountByte % 20) + 1;
        var entries = new List<KeyValuePair<string, string>>(entryCount);

        for (var i = 0; i < entryCount; i++)
        {
            var name = GenerateSatelliteName(nameSeeds, i);
            entries.Add(new KeyValuePair<string, string>(name, $"Entry_{i}"));
        }

        // Test with various "empty-ish" queries
        var queries = new[] { "", " ", "-", "- -", "  --  " };

        foreach (var query in queries)
        {
            var linearResult = LinearScanResolve(entries, query);
            var dictResult = DictionaryResolve(entries, query);
            if (linearResult != dictResult)
                return false;
        }

        return true;
    }
}
