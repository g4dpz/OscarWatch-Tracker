using OscarWatch.Core.Models;
using OscarWatch.Core.Services;

namespace OscarWatch.Tests;

/// <summary>
/// **Validates: Requirements 12.6**
///
/// Edge-case tests verifying that <see cref="SatelliteCatalogMatching.IsEnabled"/>
/// correctly handles whitespace-only entries in the enabled set.
/// </summary>
public sealed class SatelliteCatalogMatchingTests
{
    [Fact]
    public void Enabled_set_with_whitespace_only_entries_does_not_match()
    {
        var satellite = new SatelliteCatalogEntry
        {
            Name = "ISS (ZARYA)",
            NoradId = "25544",
            Line1 = "1 25544U 98067A   24001.00000000  .00000000  00000-0  00000-0 0  0000",
            Line2 = "2 25544  51.6400 000.0000 0006000 000.0000 000.0000 15.50000000 00000"
        };

        var enabledSet = new HashSet<string> { "   ", "  \t  ", " " };

        var result = SatelliteCatalogMatching.IsEnabled(satellite, enabledSet);

        Assert.False(result);
    }
}
