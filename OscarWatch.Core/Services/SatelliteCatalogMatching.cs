using OscarWatch.Core.Models;

namespace OscarWatch.Core.Services;

public static class SatelliteCatalogMatching
{
    public static bool IsEnabled(SatelliteCatalogEntry satellite, IReadOnlySet<string> enabled)
    {
        if (enabled.Contains(satellite.Name))
            return true;

        foreach (var name in enabled)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (satellite.Name.Contains(name, StringComparison.OrdinalIgnoreCase)
                || name.Contains(satellite.Name, StringComparison.OrdinalIgnoreCase))
                return true;

            if (satellite.Name.Contains($"({name})", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
