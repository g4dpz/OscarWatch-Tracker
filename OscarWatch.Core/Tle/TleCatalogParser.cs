using OscarWatch.Core.Models;

namespace OscarWatch.Core.Tle;

public static class TleCatalogParser
{
    public static IReadOnlyList<SatelliteCatalogEntry> ParseCatalog(string text) =>
        ParseCatalogWithDiagnostics(text).Entries;

    public static TleCatalogParseResult ParseCatalogWithDiagnostics(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new TleCatalogParseResult([], TleCatalogParseDiagnostics.Empty);

        var trimmed = text.TrimStart();
        if (trimmed.StartsWith("[", StringComparison.Ordinal) || trimmed.StartsWith("{", StringComparison.Ordinal))
            return GpJsonCatalogParser.ParseCatalogWithDiagnostics(text);

        return TleParser.ParseCatalogWithDiagnostics(text);
    }
}
