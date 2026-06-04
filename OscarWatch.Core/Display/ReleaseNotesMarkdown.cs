using System.Text.RegularExpressions;

namespace OscarWatch.Core.Display;

/// <summary>Prepares GitHub release markdown for in-app display.</summary>
public static partial class ReleaseNotesMarkdown
{
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

    [GeneratedRegex(@"!\[[^\]]*\]\([^)\r\n]+(?:\s+""[^""]*"")?\)", RegexOptions.None)]
    private static partial Regex MarkdownImageRegex();

    [GeneratedRegex(@"!\[[^\]]*\]\[[^\]]*\]", RegexOptions.None)]
    private static partial Regex MarkdownReferenceImageRegex();

    [GeneratedRegex(@"<img\b[^>]*\/?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlImageRegex();

    [GeneratedRegex(@"\n{3,}", RegexOptions.None)]
    private static partial Regex CollapseBlankLinesRegex();
}
