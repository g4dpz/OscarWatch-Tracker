namespace OscarWatch.Core.Tle;

public sealed record TleCatalogParseDiagnostics(
    int ParsedCount,
    int SkippedIncomplete,
    int SkippedOrbitalSanity)
{
    public int TotalRecords => ParsedCount + SkippedIncomplete + SkippedOrbitalSanity;

    public static TleCatalogParseDiagnostics Empty { get; } = new(0, 0, 0);
}

public sealed record TleCatalogParseResult(
    IReadOnlyList<Models.SatelliteCatalogEntry> Entries,
    TleCatalogParseDiagnostics Diagnostics);
