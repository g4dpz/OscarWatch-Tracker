using OscarWatch.Core.Dxcc;
using OscarWatch.Core.Logbook;
using OscarWatch.Core.Models;

namespace OscarWatch.Tests;

public class CtyDatParserTests
{
    private const string SampleCty = """
        England:                  14:  27:  EU:   52.00:     0.00:     0.0:  G:
            G,GX,M,2E,=G0TEST;
        Scotland:                 14:  27:  EU:   56.82:     4.18:     0.0:  GM:
            GM,MM,2M;
        United States:            05:  08:  NA:   37.00:    90.00:    -5.0:  K:
            K,W,N,A,=KG4AA;
        Guantanamo Bay:           08:  11:  NA:   20.00:    75.00:    -5.0:  KG4:
            KG4;
        Canary Islands:           33:  36:  AF:   28.00:    15.50:     0.0:  EA8:
            EA8;
        Sicily:                   15:  28:  EU:   37.50:   -14.00:    -1.0:  *IT9:
            IT9;
        """;

    [Fact]
    public void Parse_reads_entities_prefixes_and_exact_calls()
    {
        var db = CtyDatParser.Parse(SampleCty);

        Assert.True(db.ExactCalls.ContainsKey("G0TEST"));
        Assert.Contains(db.PrefixesByLengthDescending, p => p.Prefix == "EA8");
        Assert.Contains(db.PrefixesByLengthDescending, p => p.Prefix == "MM");
        Assert.True(db.PrefixesByLengthDescending[0].Prefix.Length
            >= db.PrefixesByLengthDescending[^1].Prefix.Length);
    }

    [Theory]
    [InlineData("G0ABC", "G", "England")]
    [InlineData("G0ABC/P", "G", "England")]
    [InlineData("W1AW/4", "K", "United States")]
    [InlineData("MM/M0VXX/P", "GM", "Scotland")]
    [InlineData("EA8/G0ABC/P", "EA8", "Canary Islands")]
    [InlineData("G0TEST", "G", "England")]
    [InlineData("KG4AA", "K", "United States")]
    [InlineData("KG4AB", "KG4", "Guantanamo Bay")]
    public void Resolver_matches_portable_and_exact_forms(string call, string primary, string name)
    {
        var resolver = new CallsignDxccResolver(CtyDatParser.Parse(SampleCty));

        Assert.True(resolver.TryMatch(call, out var match));
        Assert.Equal(primary, match.Entity.PrimaryPrefix);
        Assert.Equal(name, match.Entity.Name);
    }

    [Fact]
    public void Resolver_returns_false_for_unknown_call()
    {
        var resolver = new CallsignDxccResolver(CtyDatParser.Parse(SampleCty));
        Assert.False(resolver.TryMatch("ZZ9ZZ", out _));
    }
}

public class DxccEntityMapTests
{
    [Fact]
    public void TryResolve_maps_primary_prefix_to_adif_id()
    {
        var map = DxccEntityMap.LoadFromJson("""
            {
              "G": { "dxcc": 223, "country": "England" },
              "I": { "dxcc": 248, "country": "Italy" },
              "GM": { "dxcc": 279, "country": "Scotland" }
            }
            """);

        var england = new CtyEntity { Name = "England", PrimaryPrefix = "G" };
        Assert.True(map.TryResolve(england, out var info));
        Assert.Equal(223, info.Dxcc);
        Assert.Equal("England", info.Country);
    }

    [Fact]
    public void TryResolve_maps_wae_sicily_to_italy()
    {
        var map = DxccEntityMap.LoadFromJson("""
            {
              "I": { "dxcc": 248, "country": "Italy" },
              "IS": { "dxcc": 225, "country": "Sardinia" }
            }
            """);

        var sicily = new CtyEntity { Name = "Sicily", PrimaryPrefix = "IT9", IsWaeOnly = true };
        Assert.True(map.TryResolve(sicily, out var info));
        Assert.Equal(248, info.Dxcc);
        Assert.Equal("Italy", info.Country);
    }
}

public class DxccLookupServiceTests
{
    [Fact]
    public void TryResolve_uses_bundled_assets_when_present()
    {
        var appBase = FindAppAssetsBase();
        if (appBase is null)
            return; // Skip when assets are not copied beside the test host.

        var service = new DxccLookupService(appBase);
        Assert.True(service.TryResolve("G0ABC", out var match));
        Assert.Equal(223, match.Dxcc);
        Assert.Equal("England", match.Country);

        Assert.True(service.TryResolve("MM/M0VXX/P", out var scotland));
        Assert.Equal(279, scotland.Dxcc);
        Assert.Equal("Scotland", scotland.Country);
    }

    private static string? FindAppAssetsBase()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "cty.dat"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "OscarWatch", "Assets", "cty.dat")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "OscarWatch", "Assets", "cty.dat"))
        };

        foreach (var cty in candidates)
        {
            if (!File.Exists(cty))
                continue;
            var map = Path.Combine(Path.GetDirectoryName(cty)!, "dxcc-prefix-map.json");
            if (File.Exists(map))
                return Path.GetDirectoryName(Path.GetDirectoryName(cty))!;
        }

        return null;
    }
}

public class QsoDxccRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly QsoLogbookRepository _repository;

    public QsoDxccRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"oscarwatch-dxcc-{Guid.NewGuid():N}.db");
        _repository = new QsoLogbookRepository(_dbPath);
    }

    public void Dispose()
    {
        _repository.Dispose();
        try
        {
            File.Delete(_dbPath);
        }
        catch
        {
            // ignore
        }
    }

    [Fact]
    public async Task AddQso_persists_dxcc_and_finds_by_entity()
    {
        await _repository.InitializeAsync();
        var logbook = await _repository.CreateLogbookAsync(new QsoLogbookCreateRequest
        {
            Name = "DXCC",
            MyCallsign = "MM9SQL",
            MyGridSquare = "IO87"
        });

        await _repository.AddQsoAsync(new QsoRecordCreateRequest
        {
            LogbookId = logbook.Id,
            QsoUtc = DateTime.UtcNow,
            Call = "G0ABC",
            Dxcc = 223,
            Country = "England",
            SatName = "SO-50",
            Mode = "FM",
            Band = "2m"
        });

        var found = await _repository.FindLatestQsoForDxccAsync(logbook.Id, 223);
        Assert.NotNull(found);
        Assert.Equal(223, found.Dxcc);
        Assert.Equal("England", found.Country);

        var missing = await _repository.ListQsosMissingDxccAsync(logbook.Id);
        Assert.Empty(missing);
    }
}
