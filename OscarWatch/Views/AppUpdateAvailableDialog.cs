using Avalonia.Controls;
using OscarWatch.Core.Models;
using OscarWatch.Localization;

namespace OscarWatch.Views;

public static class AppUpdateAvailableDialog
{
    public static async Task<AppUpdateDialogResult?> TryShowAsync(
        Window owner,
        GitHubLatestRelease release,
        string currentVersionText,
        ILocalizationService localization)
    {
        var window = new AppUpdateAvailableWindow(release, currentVersionText, localization);
        return await window.ShowDialog<AppUpdateDialogResult?>(owner).ConfigureAwait(true);
    }
}
