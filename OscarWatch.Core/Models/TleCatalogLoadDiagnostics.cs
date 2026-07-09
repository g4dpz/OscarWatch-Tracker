namespace OscarWatch.Core.Models;

public enum TleLoadOrigin
{
    None,
    Cache,
    Network,
    LocalFile,
    BundledSeed
}

public sealed record TleCatalogLoadDiagnostics(
    TleLoadOrigin Origin,
    int ParsedCount,
    int SkippedIncomplete,
    int SkippedOrbitalSanity)
{
    public int TotalRecords => ParsedCount + SkippedIncomplete + SkippedOrbitalSanity;
}
