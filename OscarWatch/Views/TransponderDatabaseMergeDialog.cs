using Avalonia.Controls;
using OscarWatch.Core.Models;
using OscarWatch.Core.Services;
using OscarWatch.ViewModels;

namespace OscarWatch.Views;

public static class TransponderDatabaseMergeDialog
{
    public static async Task<bool> TryShowAsync(
        Window owner,
        SatelliteDatabaseMergePlan plan,
        ISatelliteDatabaseSyncService syncService)
    {
        var merged = await TryMergeApplyAsync(owner, plan, syncService.LoadLocalEntriesForMerge());
        if (merged is null)
            return false;

        syncService.SaveMergedEntries(merged);
        return true;
    }

    public static async Task<List<SatelliteRadioEntry>?> TryMergeApplyAsync(
        Window owner,
        SatelliteDatabaseMergePlan plan,
        IReadOnlyList<SatelliteRadioEntry> localEntries,
        SatelliteDatabaseMergePresentation presentation = SatelliteDatabaseMergePresentation.RemoteUpdate)
    {
        var vm = new SatelliteDatabaseMergeViewModel(plan, Localization.LocalizationService.Instance, presentation);
        var window = new SatelliteDatabaseMergeWindow
        {
            DataContext = vm
        };

        if (await window.ShowDialog<bool?>(owner) != true)
            return null;

        var selection = vm.BuildSelection();
        if (!vm.HasSelectedChanges())
            return null;

        return SatelliteDatabaseMerger.Apply(localEntries, plan, selection);
    }
}
