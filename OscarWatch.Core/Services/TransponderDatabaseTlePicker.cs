using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public static class TransponderDatabaseTlePicker
{
    public static IReadOnlyList<SatelliteCatalogEntry> ListAvailable(
        IReadOnlyList<SatelliteCatalogEntry> catalog,
        IEnumerable<string> existingNames)
    {
        var existing = new HashSet<string>(
            existingNames.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()),
            StringComparer.OrdinalIgnoreCase);

        return catalog
            .Where(s => !string.IsNullOrWhiteSpace(s.Name) && !existing.Contains(s.Name.Trim()))
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string? ResolveChosenName(string? selectedCatalogName, string customName)
    {
        var custom = customName.Trim();
        if (!string.IsNullOrEmpty(custom))
            return custom;

        var selected = selectedCatalogName?.Trim();
        return string.IsNullOrEmpty(selected) ? null : selected;
    }
}
