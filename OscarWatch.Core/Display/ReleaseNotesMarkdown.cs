using System.Text.RegularExpressions;

namespace OscarWatch.Core.Display;

/// <summary>Prepares GitHub release markdown for in-app display.</summary>
public static partial class ReleaseNotesMarkdown
{
    /// <summary>Prepares GitHub release markdown for the in-app release notes dialog.</summary>
    public static string PrepareForDisplay(string? markdown) =>
        ToPlainText(markdown);

    /// <summary>Converts GitHub release markdown to readable plain text for the release notes dialog.</summary>
    public static string ToPlainText(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return markdown ?? "";

        var text = StripImages(markdown);
        text = StripLeadingTitle(text);
        text = FencedCodeBlockRegex().Replace(text, m => "\n" + m.Groups[1].Value.Trim() + "\n");
        text = MarkdownLinkRegex().Replace(text, m =>
            string.IsNullOrWhiteSpace(m.Groups[2].Value)
                ? m.Groups[1].Value
                : $"{m.Groups[1].Value} ({m.Groups[2].Value})");
        text = InlineCodeRegex().Replace(text, m => m.Groups[1].Value);
        text = AtxHeadingRegex().Replace(text, m => "\n" + m.Groups[1].Value.Trim() + "\n");
        text = UnorderedListRegex().Replace(text, "• ");
        text = BoldItalicRegex().Replace(text, m =>
            m.Groups[1].Success ? m.Groups[1].Value
            : m.Groups[2].Success ? m.Groups[2].Value
            : m.Groups[3].Success ? m.Groups[3].Value
            : m.Groups[4].Value);
        text = CollapseBlankLinesRegex().Replace(text, "\n\n");
        return text.Trim();
    }

    /// <summary>Removes image markup that the in-app renderer does not handle reliably.</summary>
    public static string StripImages(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return markdown ?? "";

        var text = MarkdownImageRegex().Replace(markdown, "");
        text = MarkdownReferenceImageRegex().Replace(text, "");
        text = HtmlImageRegex().Replace(text, "");
        text = CollapseBlankLinesRegex().Replace(text, "\n\n");
        return text.Trim();
    }

    /// <summary>Removes a leading ATX H1; the dialog already shows the release title above the body.</summary>
    public static string StripLeadingTitle(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return markdown;

        return LeadingH1Regex().Replace(markdown, "", 1).TrimStart();
    }

    [GeneratedRegex(@"!\[[^\]]*\]\([^)\r\n]+(?:\s+""[^""]*"")?\)", RegexOptions.None)]
    private static partial Regex MarkdownImageRegex();

    [GeneratedRegex(@"!\[[^\]]*\]\[[^\]]*\]", RegexOptions.None)]
    private static partial Regex MarkdownReferenceImageRegex();

    [GeneratedRegex(@"<img\b[^>]*\/?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlImageRegex();

    [GeneratedRegex(@"\n{3,}", RegexOptions.None)]
    private static partial Regex CollapseBlankLinesRegex();

    [GeneratedRegex(@"^\s*#\s+[^\r\n]+(?:\r?\n)?")]
    private static partial Regex LeadingH1Regex();

    [GeneratedRegex(@"```[^\r\n]*\r?\n([\s\S]*?)```", RegexOptions.Multiline)]
    private static partial Regex FencedCodeBlockRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)\s]+)(?:\s+""[^""]*"")?\)")]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"\*\*([^*]+)\*\*|\*([^*]+)\*|__([^_]+)__|_([^_]+)_")]
    private static partial Regex BoldItalicRegex();

    [GeneratedRegex(@"^\s{0,3}#{1,6}\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex AtxHeadingRegex();

    [GeneratedRegex(@"^\s*[-*+]\s+", RegexOptions.Multiline)]
    private static partial Regex UnorderedListRegex();
}
