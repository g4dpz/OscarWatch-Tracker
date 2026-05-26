using Avalonia.Controls;
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
        var vm = new SatelliteDatabaseMergeViewModel(plan);
        var window = new SatelliteDatabaseMergeWindow
        {
            DataContext = vm
        };

        if (await window.ShowDialog<bool?>(owner) != true)
            return false;

        var selection = vm.BuildSelection();
        if (!vm.HasSelectedChanges())
            return false;

        syncService.ApplyMerge(plan, selection);
        return true;
    }
}
