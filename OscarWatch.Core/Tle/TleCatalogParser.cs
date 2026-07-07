using OscarWatch.Core.Models;

namespace OscarWatch.Core.Tle;

public static class TleCatalogParser
{
    public static IReadOnlyList<SatelliteCatalogEntry> ParseCatalog(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var trimmed = text.TrimStart();
        if (trimmed.StartsWith("[", StringComparison.Ordinal) || trimmed.StartsWith("{", StringComparison.Ordinal))
            return GpJsonCatalogParser.ParseCatalog(text);

        return TleParser.ParseCatalog(text);
    }
}
