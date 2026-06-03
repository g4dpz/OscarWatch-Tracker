using Avalonia.Controls;
using OscarWatch.Core.Services;
using OscarWatch.ViewModels;

namespace OscarWatch.Views;

public static class AddSatelliteFromTleDialog
{
    public static async Task<string?> TryPickAsync(
        Window owner,
        ITleService tleService,
        IEnumerable<string> existingNames,
        CancellationToken cancellationToken = default)
    {
        await tleService.EnsureLoadedAsync(cancellationToken).ConfigureAwait(true);
        var vm = new AddSatelliteFromTleViewModel(
            tleService.Catalog,
            existingNames,
            Localization.LocalizationService.Instance);
        var window = new AddSatelliteFromTleWindow { DataContext = vm };
        return await window.ShowDialog<string?>(owner).ConfigureAwait(true);
    }
}
