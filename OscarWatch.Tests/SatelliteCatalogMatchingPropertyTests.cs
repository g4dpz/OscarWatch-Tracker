// Feature: test-coverage-expansion, Property 31: Satellite Catalog Matching Positive
// Feature: test-coverage-expansion, Property 32: Satellite Catalog Matching Negative

using FsCheck;
using FsCheck.Xunit;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 12.1, 12.2, 12.3, 12.4, 12.5**
///
/// Property-based tests verifying that <see cref="SatelliteCatalogMatching.IsEnabled"/>
/// correctly identifies enabled satellites using exact, substring, reverse-substring,
/// and parenthetical matching strategies.
/// </summary>
public class SatelliteCatalogMatchingPropertyTests
{
    /// <summary>
    /// Property 31: Satellite Catalog Matching Positive.
    ///
    /// For any satellite name and enabled set where at least one matching strategy
    /// applies (exact, substring, reverse-substring, or parenthetical), IsEnabled
    /// returns true.
    /// </summary>
    [Property]
    public bool Positive_match_returns_true_when_any_strategy_matches(int strategyIndex, NonEmptyString baseName, NonEmptyString prefix, NonEmptyString suffix)
    {
        var baseStr = baseName.Get;
        var prefixStr = prefix.Get;
        var suffixStr = suffix.Get;

        // Skip inputs containing characters that could accidentally trigger other strategies
        if (baseStr.Contains('(') || baseStr.Contains(')'))
            return true;
        if (prefixStr.Contains('(') || prefixStr.Contains(')'))
            return true;
        if (suffixStr.Contains('(') || suffixStr.Contains(')'))
            return true;
        if (string.IsNullOrWhiteSpace(baseStr))
            return true;

        // Pick one of four strategies based on strategyIndex
        var strategy = ((strategyIndex % 4) + 4) % 4;

        string satelliteName;
        HashSet<string> enabledSet;

        switch (strategy)
        {
            case 0:
                // Strategy (a): Exact name match
                satelliteName = baseStr;
                enabledSet = new HashSet<string> { baseStr };
                break;

            case 1:
                // Strategy (b): Satellite name contains enabled entry as substring
                // Build satellite name that contains baseStr as a substring
                satelliteName = prefixStr + baseStr + suffixStr;
                enabledSet = new HashSet<string> { baseStr };
                break;

            case 2:
                // Strategy (c): Enabled entry contains satellite name as substring
                // Build enabled entry that contains satellite name as a substring
                satelliteName = baseStr;
                enabledSet = new HashSet<string> { prefixStr + baseStr + suffixStr };
                break;

            case 3:
                // Strategy (d): Satellite name contains enabled name in parentheses
                // Build satellite name like "PREFIX (baseStr) SUFFIX"
                satelliteName = prefixStr + " (" + baseStr + ") " + suffixStr;
                enabledSet = new HashSet<string> { baseStr };
                break;

            default:
                return true;
        }

        var satellite = new SatelliteCatalogEntry
        {
            Name = satelliteName,
            NoradId = "00000",
            Line1 = "1 00000U 00000A   00001.00000000  .00000000  00000-0  00000-0 0  0000",
            Line2 = "2 00000  00.0000 000.0000 0000000 000.0000 000.0000 15.00000000 00000"
        };

        return SatelliteCatalogMatching.IsEnabled(satellite, enabledSet);
    }

    /// <summary>
    /// Property 32: Satellite Catalog Matching Negative.
    ///
    /// For any satellite name and enabled set where no matching strategy applies
    /// (no exact, substring, reverse-substring, or parenthetical match), IsEnabled
    /// returns false.
    /// </summary>
    [Property]
    public bool Negative_match_returns_false_when_no_strategy_matches(NonEmptyString rawSatName, NonEmptyString rawEnabledName)
    {
        var satBase = rawSatName.Get;
        var enabledBase = rawEnabledName.Get;

        // Skip whitespace-only inputs (separate edge-case test)
        if (string.IsNullOrWhiteSpace(satBase) || string.IsNullOrWhiteSpace(enabledBase))
            return true;

        // Construct names that are guaranteed to NOT match by any strategy:
        // Use unique separators to ensure no substring relationship exists.
        // Prefix each with a unique marker that the other cannot contain.
        var satelliteName = "SAT_X_" + satBase + "_X_SAT";
        var enabledName = "EN_Y_" + enabledBase + "_Y_EN";

        // Verify our construction: satellite name must not contain enabled name
        // and enabled name must not contain satellite name (case-insensitive)
        if (satelliteName.Contains(enabledName, StringComparison.OrdinalIgnoreCase))
            return true;
        if (enabledName.Contains(satelliteName, StringComparison.OrdinalIgnoreCase))
            return true;
        // Also verify no parenthetical match
        if (satelliteName.Contains($"({enabledName})", StringComparison.OrdinalIgnoreCase))
            return true;

        var satellite = new SatelliteCatalogEntry
        {
            Name = satelliteName,
            NoradId = "00000",
            Line1 = "1 00000U 00000A   00001.00000000  .00000000  00000-0  00000-0 0  0000",
            Line2 = "2 00000  00.0000 000.0000 0000000 000.0000 000.0000 15.00000000 00000"
        };

        var enabledSet = new HashSet<string> { enabledName };

        return !SatelliteCatalogMatching.IsEnabled(satellite, enabledSet);
    }
}
