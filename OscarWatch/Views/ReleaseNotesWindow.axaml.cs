using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Markdown.Avalonia;
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
        var body = string.IsNullOrWhiteSpace(release.Body)
            ? release.TagName
            : ReleaseNotesMarkdown.PrepareForDisplay(release.Body);
        BodyMarkdown.Markdown = body;
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
