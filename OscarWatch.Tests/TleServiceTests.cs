using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
using OscarWatch.Core.Tle;

namespace OscarWatch.Tests;

public sealed class TleServiceTests
{
    [Fact]
    public void IsEnabled_matches_parenthetical_tle_name()
    {
        var catalog = TleParser.ParseCatalog("""
            SAUDISAT 1C (SO-50)
            1 27607U 02058C   26141.24923057  .00000576  00000-0  85866-4 0  9998
            2 27607  64.5520 212.3264 0075596 267.4106  91.8345 14.82983020260469
            """);

        var enabled = new HashSet<string>(["SO-50"], StringComparer.OrdinalIgnoreCase);

        Assert.True(SatelliteCatalogMatching.IsEnabled(catalog[0], enabled));
    }
}
