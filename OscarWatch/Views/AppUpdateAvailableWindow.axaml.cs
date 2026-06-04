using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OscarWatch.Core.Models;
using OscarWatch.Localization;

namespace OscarWatch.Views;

public enum AppUpdateDialogResult
{
    RemindLater,
    SkipVersion,
    ViewRelease
}

public partial class AppUpdateAvailableWindow : Window
{
    private readonly string _releaseUrl;

    public AppUpdateAvailableWindow()
    {
        InitializeComponent();
        _releaseUrl = "";
    }

    public AppUpdateAvailableWindow(
        GitHubLatestRelease release,
        string currentVersionText,
        ILocalizationService localization)
    {
        InitializeComponent();
        _releaseUrl = release.HtmlUrl;
        var latestLabel = string.IsNullOrWhiteSpace(release.Name) ? release.TagName : release.Name;
        MessageText.Text = localization.Get(
            "Update.Available.Message",
            currentVersionText,
            latestLabel,
            release.TagName);
    }

    private void OnRemindLaterClick(object? sender, RoutedEventArgs e) =>
        Close(AppUpdateDialogResult.RemindLater);

    private void OnSkipClick(object? sender, RoutedEventArgs e) =>
        Close(AppUpdateDialogResult.SkipVersion);

    private void OnViewReleaseClick(object? sender, RoutedEventArgs e)
    {
        OpenUrl(_releaseUrl);
        Close(AppUpdateDialogResult.ViewRelease);
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}
