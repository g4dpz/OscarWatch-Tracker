using OscarWatch.Core.Display;

namespace OscarWatch.Tests;

public sealed class ReleaseNotesDocumentTests
{
    [Fact]
    public void Parse_recognizes_headings_and_list_items()
    {
        const string input = """
            ## New features

            * **GPS** support
            * Serial and gpsd

            ### Details
            Paragraph text.
            """;

        var document = ReleaseNotesDocument.Parse(input);
        Assert.Collection(
            document.Blocks,
            b => Assert.Equal(ReleaseNoteBlockKind.Heading2, b.Kind),
            b =>
            {
                Assert.Equal(ReleaseNoteBlockKind.ListItem, b.Kind);
                Assert.Contains(b.Spans, s => s.Kind == ReleaseNoteSpanKind.Bold && s.Text == "GPS");
            },
            b => Assert.Equal(ReleaseNoteBlockKind.ListItem, b.Kind),
            b => Assert.Equal(ReleaseNoteBlockKind.Heading3, b.Kind),
            b => Assert.Equal(ReleaseNoteBlockKind.Paragraph, b.Kind));
    }

    [Fact]
    public void ParseInline_handles_links_and_code()
    {
        var spans = ReleaseNotesDocumentParser.ParseInline("See [Docs](https://example.com) and `gpsd`");
        Assert.Contains(spans, s => s.Kind == ReleaseNoteSpanKind.Link && s.Url == "https://example.com");
        Assert.Contains(spans, s => s.Kind == ReleaseNoteSpanKind.Code && s.Text == "gpsd");
    }
}
