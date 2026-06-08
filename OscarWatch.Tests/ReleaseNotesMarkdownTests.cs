using OscarWatch.Core.Display;

namespace OscarWatch.Tests;

public sealed class ReleaseNotesMarkdownTests
{
    [Fact]
    public void StripImages_removes_markdown_image()
    {
        const string input = "# Release\n\n![screenshot](https://example.com/a.png)\n\n* item";
        var result = ReleaseNotesMarkdown.StripImages(input);
        Assert.DoesNotContain("![screenshot]", result);
        Assert.Contains("# Release", result);
        Assert.Contains("* item", result);
    }

    [Fact]
    public void StripImages_removes_html_img_tag()
    {
        const string input = "<p>Hello</p><img src=\"https://example.com/x.jpg\" alt=\"x\" />";
        var result = ReleaseNotesMarkdown.StripImages(input);
        Assert.DoesNotContain("<img", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Hello", result);
    }

    [Fact]
    public void StripImages_returns_empty_for_null()
    {
        Assert.Equal("", ReleaseNotesMarkdown.StripImages(null));
    }

    [Fact]
    public void StripLeadingTitle_removes_first_h1()
    {
        const string input = "# OscarWatch Tracker — Version 0.8.4\n\n## New features\n* item";
        var result = ReleaseNotesMarkdown.StripLeadingTitle(input);
        Assert.DoesNotContain("OscarWatch Tracker", result);
        Assert.StartsWith("## New features", result);
    }

    [Fact]
    public void PrepareForDisplay_strips_title_and_images()
    {
        const string input = "# Release\n\n![shot](https://example.com/a.png)\n\n## Section";
        var result = ReleaseNotesMarkdown.PrepareForDisplay(input);
        Assert.DoesNotContain("# Release", result);
        Assert.DoesNotContain("![shot]", result);
        Assert.Contains("Section", result);
    }

    [Fact]
    public void ToPlainText_converts_markdown_basics()
    {
        const string input = "## New features\n\n* **GPS** support\n\n[Docs](https://example.com/docs)";
        var result = ReleaseNotesMarkdown.ToPlainText(input);
        Assert.Contains("New features", result);
        Assert.Contains("• GPS support", result);
        Assert.Contains("Docs (https://example.com/docs)", result);
        Assert.DoesNotContain("**", result);
    }
}
