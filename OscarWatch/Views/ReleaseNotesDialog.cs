using Avalonia.Controls;
using OscarWatch.Core.Models;

namespace OscarWatch.Views;

public static class ReleaseNotesDialog
{
    public static async Task ShowAsync(Window owner, GitHubLatestRelease release)
    {
        var window = new ReleaseNotesWindow(release);
        await window.ShowDialog(owner).ConfigureAwait(true);
    }
}
