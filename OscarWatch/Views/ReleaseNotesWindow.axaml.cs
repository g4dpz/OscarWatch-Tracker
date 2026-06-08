using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OscarWatch.Core.Display;
using OscarWatch.Core.Models;

namespace OscarWatch.Views;

public partial class ReleaseNotesWindow : Window
{
    private readonly string _htmlUrl;

    public ReleaseNotesWindow()
    {
        InitializeComponent();
        _htmlUrl = "";
    }

    public ReleaseNotesWindow(GitHubLatestRelease release)
    {
        InitializeComponent();
        _htmlUrl = release.HtmlUrl;
        HeadingText.Text = string.IsNullOrWhiteSpace(release.Name)
            ? release.TagName
            : release.Name;
        var document = string.IsNullOrWhiteSpace(release.Body)
            ? ReleaseNotesDocument.Parse(release.TagName)
            : ReleaseNotesDocument.Parse(release.Body);
        ReleaseNotesContentBuilder.Populate(BodyPanel, document);
        Title = HeadingText.Text ?? Title;
    }

    private void OnViewOnGitHubClick(object? sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo
        {
            FileName = _htmlUrl,
            UseShellExecute = true
        });

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
