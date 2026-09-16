using OscarWatch.Core.Display;

namespace OscarWatch.ViewModels;

/// <summary>
/// Sidebar pass-list selection: keep the clicked row (including a later pass of the same
/// satellite) instead of snapping to that NORAD's first upcoming row.
/// </summary>
internal static class PassListSelection
{
    public static bool IsRowForSatellite(IPassListItem? item, string? noradId) =>
        item is PassRowViewModel row
        && !string.IsNullOrEmpty(noradId)
        && string.Equals(row.NoradId, noradId, StringComparison.Ordinal);

    public static PassRowViewModel? FindMatchingRow(
        IEnumerable<IPassListItem> items,
        string noradId,
        DateTime? aosUtc)
    {
        var rows = items.OfType<PassRowViewModel>();
        if (aosUtc is DateTime aos)
        {
            var aosNorm = PassUtc.Normalize(aos);
            var exact = rows.FirstOrDefault(p =>
                string.Equals(p.NoradId, noradId, StringComparison.Ordinal)
                && PassUtc.Normalize(p.AosUtc) == aosNorm);
            if (exact is not null)
                return exact;
        }

        return rows.FirstOrDefault(p =>
            string.Equals(p.NoradId, noradId, StringComparison.Ordinal));
    }
}
