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
}
