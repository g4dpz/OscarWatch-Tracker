using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using OscarWatch.Core.Display;
using OscarWatch.Core.Models;
using OscarWatch.Views;

namespace OscarWatch.Tests;

public sealed class ReleaseNotesDialogTests
{
    private const string SampleReleaseBody = """
        # v0.8.5

        ## New Hardware and GPS Support

        * **Network gpsd Support** — Connect to a `gpsd` daemon on TCP port 2947.
        * **Serial GPS and NMEA Parsing** — Built-in GGA/RMC parser for COM-port GPS.
        * **Yaesu FTX-1 Dual-Radio Driver** — VFO-A semantics and FM dial lock.

        ## Online Integrations

        * **hams.at** roves in the sidebar.
        """;

    [Fact]
    public void Populate_renders_realistic_release_body()
    {
        var document = ReleaseNotesDocument.Parse(SampleReleaseBody);
        var panel = new StackPanel();

        ReleaseNotesContentBuilder.Populate(panel, document);

        Assert.True(panel.Children.Count >= 5);
        Assert.Contains(panel.Children, c => c is TextBlock { FontWeight: FontWeight.SemiBold });
        Assert.Contains(panel.Children, c => c is Grid);
        Assert.DoesNotContain(panel.Children, c => c is TextBlock tb && tb.Text!.Contains("**"));
    }

    [Fact]
    public void Populate_produces_bold_inline_spans_in_list_items()
    {
        var document = ReleaseNotesDocument.Parse("* **Network gpsd Support** — details.");
        var panel = new StackPanel();
        ReleaseNotesContentBuilder.Populate(panel, document);

        var listGrid = Assert.Single(panel.Children.OfType<Grid>());
        var content = listGrid.Children.OfType<TextBlock>().Single(tb => Grid.GetColumn(tb) == 1);
        Assert.Contains(content.Inlines!, inline => inline is Run { FontWeight: FontWeight.SemiBold });
    }

    [Fact]
    public void Release_pipeline_handles_typical_github_release()
    {
        var release = new GitHubLatestRelease
        {
            TagName = "v0.8.5",
            Name = "v0.8.5",
            HtmlUrl = "https://github.com/example/OscarWatch-Tracker/releases/tag/v0.8.5",
            Body = SampleReleaseBody
        };

        var document = string.IsNullOrWhiteSpace(release.Body)
            ? ReleaseNotesDocument.Parse(release.TagName)
            : ReleaseNotesDocument.Parse(release.Body);
        var panel = new StackPanel();
        ReleaseNotesContentBuilder.Populate(panel, document);

        Assert.True(panel.Children.Count >= 5);
        Assert.Contains(panel.Children, c => c is TextBlock { Text: "New Hardware and GPS Support" });
    }

    [Fact]
    public void Parse_and_populate_succeeds_for_empty_body()
    {
        var document = ReleaseNotesDocument.Parse("");
        var panel = new StackPanel();
        ReleaseNotesContentBuilder.Populate(panel, document);
        Assert.Empty(panel.Children);
    }
}
