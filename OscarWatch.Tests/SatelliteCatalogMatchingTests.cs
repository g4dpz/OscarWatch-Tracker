using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 12.6**
///
/// Edge-case tests verifying that <see cref="SatelliteCatalogMatching.IsEnabled"/>
/// correctly handles whitespace-only entries and avoids mid-token false positives.
/// </summary>
public sealed class SatelliteCatalogMatchingTests
{
    [Fact]
    public void Enabled_set_with_whitespace_only_entries_does_not_match()
    {
        var satellite = Entry("ISS (ZARYA)", "25544");
        var enabledSet = new HashSet<string> { "   ", "  \t  ", " " };

        Assert.False(SatelliteCatalogMatching.IsEnabled(satellite, enabledSet));
    }

    [Fact]
    public void OrigamiSat_2_does_not_enable_ISAT_via_mid_token_substring()
    {
        // "OrigamiSat" contains the letters i-S-a-t, which previously matched "ISAT".
        var isat = Entry("ISAT", "43879");
        var origami = Entry("OrigamiSat 2", "68795");
        var enabled = new HashSet<string>(["OrigamiSat 2"], StringComparer.OrdinalIgnoreCase);

        Assert.True(SatelliteCatalogMatching.IsEnabled(origami, enabled));
        Assert.False(SatelliteCatalogMatching.IsEnabled(isat, enabled));
    }

    [Fact]
    public void Parenthetical_alias_still_matches_SO_50()
    {
        var so50 = Entry("SAUDISAT 1C (SO-50)", "27607");
        var enabled = new HashSet<string>(["SO-50"], StringComparer.OrdinalIgnoreCase);

        Assert.True(SatelliteCatalogMatching.IsEnabled(so50, enabled));
    }

    [Fact]
    public void ContainsToken_requires_non_alnum_boundaries()
    {
        Assert.True(SatelliteCatalogMatching.ContainsToken("SAUDISAT 1C (SO-50)", "SO-50"));
        Assert.True(SatelliteCatalogMatching.ContainsToken("ISAT (CUBE)", "ISAT"));
        Assert.False(SatelliteCatalogMatching.ContainsToken("OrigamiSat 2", "ISAT"));
    }

    private static SatelliteCatalogEntry Entry(string name, string noradId) => new()
    {
        Name = name,
        NoradId = noradId,
        Line1 = $"1 {noradId}U 00000A   00001.00000000  .00000000  00000-0  00000-0 0  0000",
        Line2 = $"2 {noradId}  00.0000 000.0000 0000000 000.0000 000.0000 15.00000000 00000"
    };
}
