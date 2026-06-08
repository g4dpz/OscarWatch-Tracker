using System.Text.RegularExpressions;

namespace OscarWatch.Core.Display;

public enum ReleaseNoteBlockKind
{
    Heading2,
    Heading3,
    Paragraph,
    ListItem,
    OrderedListItem,
    CodeBlock
}

public enum ReleaseNoteSpanKind
{
    Plain,
    Bold,
    Italic,
    Code,
    Link
}

public sealed record ReleaseNoteSpan(ReleaseNoteSpanKind Kind, string Text, string? Url = null);

public sealed record ReleaseNoteBlock(ReleaseNoteBlockKind Kind, IReadOnlyList<ReleaseNoteSpan> Spans)
{
    public static ReleaseNoteBlock Plain(ReleaseNoteBlockKind kind, string text) =>
        new(kind, [new ReleaseNoteSpan(ReleaseNoteSpanKind.Plain, text)]);
}

public sealed class ReleaseNotesDocument
{
    public ReleaseNotesDocument(IReadOnlyList<ReleaseNoteBlock> blocks) =>
        Blocks = blocks;

    public IReadOnlyList<ReleaseNoteBlock> Blocks { get; }

    public static ReleaseNotesDocument Parse(string? markdown) =>
        ReleaseNotesDocumentParser.Parse(markdown);
}

public static partial class ReleaseNotesDocumentParser
{
    public static ReleaseNotesDocument Parse(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return new ReleaseNotesDocument([]);

        var text = ReleaseNotesMarkdown.StripImages(markdown);
        text = ReleaseNotesMarkdown.StripLeadingTitle(text);
        text = text.Replace("\r\n", "\n");

        var blocks = new List<ReleaseNoteBlock>();
        var lines = text.Split('\n');
        var codeLines = new List<string>();
        var inCode = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                if (inCode)
                {
                    blocks.Add(ReleaseNoteBlock.Plain(
                        ReleaseNoteBlockKind.CodeBlock,
                        string.Join('\n', codeLines).TrimEnd()));
                    codeLines.Clear();
                    inCode = false;
                }
                else
                {
                    inCode = true;
                }

                continue;
            }

            if (inCode)
            {
                codeLines.Add(line);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (TryParseHeading(line, out var headingKind, out var headingText))
            {
                blocks.Add(ReleaseNoteBlock.Plain(headingKind, headingText));
                continue;
            }

            if (UnorderedListLineRegex().IsMatch(line))
            {
                var item = UnorderedListLineRegex().Replace(line, "").Trim();
                blocks.Add(new ReleaseNoteBlock(ReleaseNoteBlockKind.ListItem, ParseInlineSpans(item)));
                continue;
            }

            if (OrderedListLineRegex().IsMatch(line))
            {
                var item = OrderedListLineRegex().Replace(line, "").Trim();
                blocks.Add(new ReleaseNoteBlock(ReleaseNoteBlockKind.OrderedListItem, ParseInlineSpans(item)));
                continue;
            }

            blocks.Add(new ReleaseNoteBlock(ReleaseNoteBlockKind.Paragraph, ParseInlineSpans(line.Trim())));
        }

        if (inCode && codeLines.Count > 0)
        {
            blocks.Add(ReleaseNoteBlock.Plain(
                ReleaseNoteBlockKind.CodeBlock,
                string.Join('\n', codeLines).TrimEnd()));
        }

        return new ReleaseNotesDocument(blocks);
    }

    public static IReadOnlyList<ReleaseNoteSpan> ParseInline(string text) =>
        ParseInlineSpans(text);

    private static bool TryParseHeading(string line, out ReleaseNoteBlockKind kind, out string text)
    {
        kind = ReleaseNoteBlockKind.Paragraph;
        text = "";

        var match = AtxHeadingLineRegex().Match(line);
        if (!match.Success)
            return false;

        var level = match.Groups[1].Value.Length;
        kind = level <= 2 ? ReleaseNoteBlockKind.Heading2 : ReleaseNoteBlockKind.Heading3;
        text = match.Groups[2].Value.Trim();
        return true;
    }

    private static IReadOnlyList<ReleaseNoteSpan> ParseInlineSpans(string text)
    {
        if (string.IsNullOrEmpty(text))
            return [new ReleaseNoteSpan(ReleaseNoteSpanKind.Plain, "")];

        var spans = new List<ReleaseNoteSpan>();
        var index = 0;
        while (index < text.Length)
        {
            if (TryMatch(text, index, LinkRegex(), out var linkMatch))
            {
                spans.Add(new ReleaseNoteSpan(
                    ReleaseNoteSpanKind.Link,
                    linkMatch.Groups[1].Value,
                    linkMatch.Groups[2].Value));
                index = linkMatch.Index + linkMatch.Length;
                continue;
            }

            if (TryMatch(text, index, InlineCodeRegex(), out var codeMatch))
            {
                spans.Add(new ReleaseNoteSpan(ReleaseNoteSpanKind.Code, codeMatch.Groups[1].Value));
                index = codeMatch.Index + codeMatch.Length;
                continue;
            }

            if (TryMatch(text, index, BoldRegex(), out var boldMatch))
            {
                spans.Add(new ReleaseNoteSpan(
                    ReleaseNoteSpanKind.Bold,
                    boldMatch.Groups[1].Success ? boldMatch.Groups[1].Value : boldMatch.Groups[2].Value));
                index = boldMatch.Index + boldMatch.Length;
                continue;
            }

            if (TryMatch(text, index, ItalicRegex(), out var italicMatch))
            {
                spans.Add(new ReleaseNoteSpan(
                    ReleaseNoteSpanKind.Italic,
                    italicMatch.Groups[1].Success ? italicMatch.Groups[1].Value : italicMatch.Groups[2].Value));
                index = italicMatch.Index + italicMatch.Length;
                continue;
            }

            var nextSpecial = FindNextSpecialIndex(text, index);
            spans.Add(new ReleaseNoteSpan(
                ReleaseNoteSpanKind.Plain,
                text[index..nextSpecial]));
            index = nextSpecial;
        }

        return spans;
    }

    private static int FindNextSpecialIndex(string text, int start)
    {
        var indices = new[]
        {
            IndexOfRegex(text, start, LinkRegex()),
            IndexOfRegex(text, start, InlineCodeRegex()),
            IndexOfRegex(text, start, BoldRegex()),
            IndexOfRegex(text, start, ItalicRegex())
        }.Where(i => i >= 0).DefaultIfEmpty(text.Length).Min();

        return indices;
    }

    private static int IndexOfRegex(string text, int start, Regex regex)
    {
        var slice = text[start..];
        var match = regex.Match(slice);
        return match.Success ? start + match.Index : -1;
    }

    private static bool TryMatch(string text, int start, Regex regex, out Match match)
    {
        match = regex.Match(text, start);
        return match.Success && match.Index == start;
    }

    [GeneratedRegex(@"^\s{0,3}(#{1,6})\s+(.+)$")]
    private static partial Regex AtxHeadingLineRegex();

    [GeneratedRegex(@"^\s*[-*+]\s+")]
    private static partial Regex UnorderedListLineRegex();

    [GeneratedRegex(@"^\s*\d+\.\s+")]
    private static partial Regex OrderedListLineRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)\s]+)(?:\s+""[^""]*"")?\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"\*\*([^*]+)\*\*|__([^_]+)__")]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"(?<!\*)\*([^*]+)\*(?!\*)|(?<!_)_([^_]+)_(?!_)")]
    private static partial Regex ItalicRegex();
}
